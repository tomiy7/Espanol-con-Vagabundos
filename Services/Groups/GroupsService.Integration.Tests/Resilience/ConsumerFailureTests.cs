using System.Net;
using System.Text;
using GroupsService.Infrastructure.Data;
using GroupsService.Infrastructure.Messaging;
using GroupsService.Infrastructure.Outbox;
using GroupsService.Integration.Tests.Infrastructure;
using Messaging;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CourseCompletedEvent = Messaging.Contracts.CourseCompleted;

namespace GroupsService.Integration.Tests.Resilience;

// Sta radi potrosac kad obrada poruke pukne: prolazna greska (ponovi), izgubljena trka
// (ponovi sa svezim podacima), trajna greska (dead-letter red, ostale poruke idu dalje),
// i poruke cudnog oblika. Varijanta API-ja slusa svoj exchange/red, pa glavni API ne
// vidi ove poruke; MaxDeliveryAttempts je 3 (ApiFactory).
public class ConsumerFailureTests : ApiTestBase
{
    private const string Exchange = "coursesite-tests-failures.events";
    private const string Queue = "groups-tests-failures";
    private const string DeadQueue = Queue + ".dead";
    private const int MaxAttempts = 3;

    public ConsumerFailureTests(ApiFactory factory) : base(factory) { }

    private async Task<(WebApplicationFactory<Program> Api, TestBus Bus, HandlerFaults Faults)> StartConsumer()
    {
        var bus = await TestBus.ConnectAsync(Exchange);
        await bus.DeleteQueueAsync(Queue);
        await bus.DeleteQueueAsync(DeadQueue);

        var faults = new HandlerFaults();
        var api = Factory.Variant(services =>
        {
            ApiFactory.UseMessaging(services, Exchange, Queue);
            ApiFactory.RemoveHostedService<OutboxDispatcher>(services);
            services.AddSingleton(faults);
            services.AddScoped<CourseCompletedEventHandler>();
            services.RemoveAll<IIntegrationEventHandler<CourseCompletedEvent>>();
            services.AddScoped<IIntegrationEventHandler<CourseCompletedEvent>, FaultyCourseCompletedHandler>();
        });
        _ = api.Server; // pokrece host, pa i potrosaca

        // Red i vezivanja pravi potrosac na startu; poruka poslata pre toga bi se izgubila.
        await WaitForConsumer();
        return (api, bus, faults);
    }

    // Ceka da se potrosac zakaci na red. Samo postojanje reda nije dovoljno: vezivanje za
    // exchange ide posle deklaracije reda, pa bi poruka objavljena izmedju ta dva koraka
    // otisla nikud.
    private static async Task WaitForConsumer()
    {
        var until = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < until)
        {
            var probe = await TestBus.ConnectAsync(Exchange); // pasivna provera zatvara kanal ako reda nema
            try
            {
                if (await probe.ConsumerCountAsync(Queue) > 0)
                    return;
                await Task.Delay(200);
            }
            catch (Exception)
            {
                await Task.Delay(200);
            }
            finally
            {
                await probe.DisposeAsync();
            }
        }
        Assert.Fail("The consumer did not attach to its queue.");
    }

    private async Task<(TestUser professor, List<TestUser> students, List<Guid> ids, Guid groupId)> ActiveGroup()
    {
        var (groupId, professor, students, ids) = await ReadyGroup();
        await AssertStatus(HttpStatusCode.NoContent, await Post(professor, $"/groups/{groupId}/start"));
        return (professor, students, ids, groupId);
    }

    private string Status(Guid enrollmentId) =>
        Factory.WithDb(db => db.Enrollments.AsNoTracking().Single(e => e.Id == enrollmentId).Status.ToString());

    [MessagingFact]
    public async Task Transient_failures_are_retried_and_the_message_is_processed()
    {
        var (_, students, ids, _) = await ActiveGroup();
        var (api, bus, faults) = await StartConsumer();
        await using var _api = api;
        await using var _bus = bus;
        faults.FailFirstAttempts = MaxAttempts - 1;

        await bus.PublishAsync(new CourseCompletedEvent(ids[0], students[0].Id, CourseId));

        await Eventually(() => Status(ids[0]) == "Completed", "the enrollment is completed after retries");
        Assert.Equal(MaxAttempts, faults.AttemptsFor(ids[0]));
        Assert.Equal(0u, await bus.MessageCountAsync(DeadQueue));
    }

    [MessagingFact]
    public async Task A_lost_concurrency_race_is_retried_with_fresh_data()
    {
        var (_, students, ids, groupId) = await ActiveGroup();
        var (api, bus, faults) = await StartConsumer();
        await using var _api = api;
        await using var _bus = bus;

        // Prava trka: handler je vec procitao grupu, a pre njegovog snimanja drugi proces
        // menja istu grupu (verzija raste). Prvi pokusaj mora da izgubi, drugi da uspe.
        faults.BeforeFirstAttempt = async (scopeServices, _) =>
        {
            var db = scopeServices.GetRequiredService<GroupDbContext>();
            await db.Groups.Include(g => g.Members).SingleAsync(g => g.Id == groupId);
            await Factory.ExecuteSqlAsync($"UPDATE groups SET version = version + 1 WHERE id = '{groupId}'");
        };

        await bus.PublishAsync(new CourseCompletedEvent(ids[0], students[0].Id, CourseId));

        await Eventually(() => Status(ids[0]) == "Completed", "the enrollment is completed after the retry");
        Assert.Equal(2, faults.AttemptsFor(ids[0]));
        Assert.Equal(2, faults.HandlerInstances.Distinct().Count()); // svaki pokusaj u novom scope-u
        Assert.Equal(0u, await bus.MessageCountAsync(DeadQueue));
    }

    [MessagingFact]
    public async Task A_message_that_always_fails_goes_to_the_dead_letter_queue_and_others_still_flow()
    {
        var (_, students, ids, _) = await ActiveGroup();
        var (api, bus, faults) = await StartConsumer();
        await using var _api = api;
        await using var _bus = bus;
        faults.AlwaysFail.Add(ids[0]);

        await bus.PublishAsync(new CourseCompletedEvent(ids[0], students[0].Id, CourseId));
        await bus.PublishAsync(new CourseCompletedEvent(ids[1], students[1].Id, CourseId));

        var dead = await bus.ReceiveAsync(DeadQueue, TimeSpan.FromSeconds(15));
        Assert.NotNull(dead);
        Assert.Contains(ids[0].ToString(), dead);
        Assert.Equal(MaxAttempts, faults.AttemptsFor(ids[0]));

        await Eventually(() => Status(ids[1]) == "Completed", "the next message is still processed");
        Assert.Equal("Active", Status(ids[0])); // neuspela obrada nije ostavila pola promene
    }

    [MessagingFact]
    public async Task Messages_with_a_wrong_shape_are_dead_lettered_or_ignored_without_blocking_the_queue()
    {
        var (_, students, ids, _) = await ActiveGroup();
        var (api, bus, faults) = await StartConsumer();
        await using var _api = api;
        await using var _bus = bus;

        Task Raw(string eventType, string data) => bus.PublishRawAsync("CourseCompleted", Encoding.UTF8.GetBytes(
            $$"""{"eventId":"{{Guid.NewGuid()}}","eventType":"{{eventType}}","occurredAt":"2026-01-01T00:00:00Z","data":""" + data + "}"));

        // Polja fale -> prazni Guid-ovi -> handler takvu poruku ignorise (nema sta da zavrsi).
        await Raw("CourseCompleted", "{}");
        // Pogresan tip polja -> ne moze da se procita -> dead-letter.
        await Raw("CourseCompleted", """{"enrollmentId":"not-a-guid"}""");
        // Nepoznat tip dogadjaja u omotacu -> dead-letter.
        await Raw("SomethingElse", "{}");
        // Ispravna poruka posle njih i dalje prolazi.
        await bus.PublishAsync(new CourseCompletedEvent(ids[0], students[0].Id, CourseId));

        await Eventually(() => Status(ids[0]) == "Completed", "a valid message after the bad ones is processed");
        await Eventually(() => bus.MessageCountAsync(DeadQueue).GetAwaiter().GetResult() == 2,
            "the two unreadable messages are dead-lettered");
        Assert.Equal(1, faults.AttemptsFor(Guid.Empty)); // poruka bez polja je obradjena (i ignorisana) jednom
    }
}
