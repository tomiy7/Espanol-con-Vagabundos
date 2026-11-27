using System.Net;
using GroupsService.Application.Interfaces;
using GroupsService.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GroupsService.Integration.Tests.Resilience;

// Greske koje nisu poslovne (bag, pad baze usred snimanja): klijent dobija 500 sa
// stabilnim kodom i bez internih detalja, a podaci ostaju kakvi su bili pre zahteva.
public class UnexpectedErrorTests : ApiTestBase
{
    public UnexpectedErrorTests(ApiFactory factory) : base(factory) { }

    private const string Secret = "internal-detail-that-must-not-leak";

    private sealed class ThrowingUserDirectory : IUserDirectory
    {
        public Task<UserContact?> GetUserContactAsync(Guid userId, CancellationToken ct = default) =>
            throw new InvalidOperationException(Secret);
    }

    private static async Task AssertInternalErrorWithoutDetails(HttpResponseMessage response, params string[] mustNotContain)
    {
        await AssertProblem(HttpStatusCode.InternalServerError, "INTERNAL_ERROR", response);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("An unexpected error occurred.", (await Json(response)).GetProperty("detail").GetString());
        foreach (var text in mustNotContain.Append(" at ").Append("Exception"))
            Assert.DoesNotContain(text, body);
    }

    [IntegrationFact]
    public async Task A_bug_in_a_dependency_returns_500_without_internal_details()
    {
        var professor = TestUser.Professor();
        var groupId = await CreateGroup(professor);
        await AddMember(professor, groupId, TestUser.Student()); // detalji grupe traze ime clana
        await using var api = Factory.Variant(services =>
        {
            services.RemoveAll<IUserDirectory>();
            services.AddSingleton<IUserDirectory, ThrowingUserDirectory>();
        });

        var response = await ClientFor(api, professor).GetAsync($"/groups/{groupId}");

        await AssertInternalErrorWithoutDetails(response, Secret, nameof(InvalidOperationException));
    }

    [IntegrationFact]
    public async Task Database_failure_in_the_middle_of_a_save_rolls_back_the_change_and_its_events()
    {
        var (groupId, professor, _, ids) = await ReadyGroup();
        var eventsBefore = Factory.WithDb(db => db.OutboxMessages.Count());

        // Baza odbija upis u outbox: poslovna promena je u istoj transakciji, pa ni ona
        // ne sme da ostane (inace bi grupa krenula, a niko ne bi saznao).
        await Factory.ExecuteSqlAsync("""
            CREATE FUNCTION test_fail_outbox() RETURNS trigger AS $$
            BEGIN RAISE EXCEPTION 'injected outbox failure'; END $$ LANGUAGE plpgsql;
            CREATE TRIGGER test_fail_outbox BEFORE INSERT ON outbox_messages
                FOR EACH ROW EXECUTE FUNCTION test_fail_outbox();
            """);
        HttpResponseMessage failed;
        try
        {
            failed = await Post(professor, $"/groups/{groupId}/start");
        }
        finally
        {
            await Factory.ExecuteSqlAsync("""
                DROP TRIGGER IF EXISTS test_fail_outbox ON outbox_messages;
                DROP FUNCTION IF EXISTS test_fail_outbox();
                """);
        }

        await AssertInternalErrorWithoutDetails(failed, "injected outbox failure", "outbox_messages", "Npgsql");
        Assert.Equal("Ready", (await GetGroup(professor, groupId)).GetProperty("status").GetString());
        Assert.All(ids, id => Assert.Equal("Ready",
            Factory.WithDb(db => db.Enrollments.AsNoTracking().Single(e => e.Id == id).Status.ToString())));
        Assert.Equal(eventsBefore, Factory.WithDb(db => db.OutboxMessages.Count()));

        // Kad baza opet prima upise, ista operacija prolazi (nista nije ostalo "zaglavljeno").
        await AssertStatus(HttpStatusCode.NoContent, await Post(professor, $"/groups/{groupId}/start"));
        // GroupConfirmed (start bez planiranog datuma potvrdjuje grupu) + GroupStarted
        Assert.Equal(eventsBefore + 2, Factory.WithDb(db => db.OutboxMessages.Count()));
    }
}
