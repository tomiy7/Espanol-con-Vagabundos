using System.Diagnostics;
using System.Net;
using GroupsService.Infrastructure.Outbox;
using GroupsService.Integration.Tests.Infrastructure;
using Messaging.RabbitMq;

namespace GroupsService.Integration.Tests.Resilience;

// Baza je nedostupna: API ne visi i ne odaje konekcioni string, vraca 500 sa kodom,
// a /health javlja da servis nije zdrav (Docker/orkestrator to vidi).
public class DatabaseOutageTests : ApiTestBase
{
    public DatabaseOutageTests(ApiFactory factory) : base(factory) { }

    private const string Password = "unreachable-db-password";
    private const string UnreachableDatabase =
        $"Host=127.0.0.1;Port=1;Database=groups_unreachable;Username=groups;Password={Password}";

    [IntegrationFact]
    public async Task Requests_fail_fast_with_500_and_health_reports_unhealthy()
    {
        await using var api = Factory.Variant(services =>
        {
            ApiFactory.UseDatabase(services, UnreachableDatabase);
            ApiFactory.RemoveHostedService<OutboxDispatcher>(services);
            ApiFactory.RemoveHostedService<RabbitMqConsumerService>(services);
        });
        var admin = TestUser.Admin();

        var timer = Stopwatch.StartNew();
        var read = await ClientFor(api, admin).GetAsync("/groups");
        var write = await ClientFor(api, admin).PostAsync("/groups",
            System.Net.Http.Json.JsonContent.Create(new { courseId = CourseId, slots = Slots(admin.Id) }));
        timer.Stop();

        foreach (var response in new[] { read, write })
        {
            await AssertProblem(HttpStatusCode.InternalServerError, "INTERNAL_ERROR", response);
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain(Password, body);
            Assert.DoesNotContain("127.0.0.1", body);
            Assert.DoesNotContain("Npgsql", body);
        }
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(20), $"Two requests took {timer.Elapsed.TotalSeconds:F1}s.");

        var health = await api.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, health.StatusCode);
    }
}
