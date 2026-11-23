using System.Net;
using System.Text.Json;
using GroupsService.Integration.Tests.Infrastructure;
using Messaging.RabbitMq;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace GroupsService.Integration.Tests.Resilience;

// Broker je nedostupan: korisnik to ne sme da oseti (operacije prolaze), dogadjaji cekaju
// u outbox-u sa zabelezenom greskom, a kad broker opet radi - odlaze redom.
// Varijanta ima svoju bazu, da glavni API (ciji broker radi) ne posalje te poruke umesto nje.
public class OutboxFailureTests : ApiTestBase
{
    private const string Database = "groups_integration_tests_outbox";
    private const string Exchange = "coursesite-tests-outbox.events";
    private const string Queue = "groups-tests-outbox";
    private const int ClosedPort = 1;

    public OutboxFailureTests(ApiFactory factory) : base(factory) { }

    private WebApplicationFactory<Program> Api(string connectionString, int? port) => Factory.Variant(services =>
    {
        ApiFactory.UseDatabase(services, connectionString);
        ApiFactory.UseMessaging(services, Exchange, Queue, port);
        ApiFactory.RemoveHostedService<RabbitMqConsumerService>(services);
    });

    private static List<(string Type, bool Processed, int Attempts, string? LastError)> Outbox(WebApplicationFactory<Program> api) =>
        ApiFactory.WithDb(api, db => db.OutboxMessages.AsNoTracking().OrderBy(m => m.OccurredAt)
            .Select(m => new { m.Type, m.ProcessedAt, m.Attempts, m.LastError }).ToList())
            .Select(m => (m.Type, m.ProcessedAt != null, m.Attempts, m.LastError)).ToList();

    [MessagingFact]
    public async Task Broker_outage_does_not_block_users_and_events_are_sent_in_order_when_it_is_back()
    {
        var connectionString = await Factory.CreateDatabaseAsync(Database);
        try
        {
            var professor = TestUser.Professor();
            Guid groupId;

            // 1) Broker nedostupan.
            await using (var down = Api(connectionString, ClosedPort))
            {
                var client = ClientFor(down, professor);
                var created = await client.PostAsync("/groups", JsonContent(new { courseId = CourseId, slots = Slots(professor.Id) }));
                await AssertStatus(HttpStatusCode.Created, created);
                groupId = (await Json(created)).GetProperty("groupId").GetGuid();
                for (var i = 0; i < 3; i++)
                    await AssertStatus(HttpStatusCode.Created,
                        await client.PostAsync($"/groups/{groupId}/members", JsonContent(new { userId = TestUser.Student().Id })));
                await AssertStatus(HttpStatusCode.NoContent, await client.PostAsync($"/groups/{groupId}/start", JsonContent(new { })));

                await Eventually(() => Outbox(down).Any(m => m.Attempts > 0), "the dispatcher has tried to send", 20);
                var pending = Outbox(down);
                Assert.Equal(new[] { "GroupBecameReady", "GroupStarted" }, pending.Select(m => m.Type));
                Assert.All(pending, m => Assert.False(m.Processed));
                Assert.False(string.IsNullOrWhiteSpace(pending[0].LastError));

                var health = await down.CreateClient().GetAsync("/health");
                Assert.Equal(HttpStatusCode.ServiceUnavailable, health.StatusCode);
            }

            // 2) Broker ponovo radi (novi proces sa ispravnom adresom, ista baza): zaostale
            //    poruke odlaze, redom kojim su nastale.
            await using var bus = await TestBus.ConnectAsync(Exchange);
            var queue = await bus.ListenAsync("GroupBecameReady", "GroupStarted");
            await using var up = Api(connectionString, port: null);
            _ = up.Server;

            await Eventually(() => Outbox(up).All(m => m.Processed), "the backlog is sent");
            var received = new List<JsonElement>();
            for (var i = 0; i < 2; i++)
            {
                var message = await bus.ReceiveAsync(queue, TimeSpan.FromSeconds(5));
                Assert.NotNull(message);
                using var envelope = JsonDocument.Parse(message);
                received.Add(envelope.RootElement.Clone());
            }
            Assert.Equal(new[] { "GroupBecameReady", "GroupStarted" }, received.Select(m => m.GetProperty("eventType").GetString()));
            Assert.All(received, m => Assert.Equal(groupId, m.GetProperty("data").GetProperty("groupId").GetGuid()));
        }
        finally
        {
            await Factory.DropDatabaseAsync(Database);
        }
    }

    private static HttpContent JsonContent(object body) => System.Net.Http.Json.JsonContent.Create(body);
}
