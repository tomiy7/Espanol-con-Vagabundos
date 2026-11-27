using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GroupsService.Integration.Tests.Infrastructure;

namespace GroupsService.Integration.Tests.Flows;

// PUT /groups/{id}/planned-start: datum prvog casa, potvrda grupe i dogadjaji za Payments.
public class PlannedStartTests : ApiTestBase
{
    public PlannedStartTests(ApiFactory factory) : base(factory) { }

    private static readonly DateTime FirstLesson = DateTime.UtcNow.Date.AddDays(14).AddHours(17);

    private Task<HttpResponseMessage> Schedule(TestUser? user, Guid groupId, object plannedStartAt) =>
        ClientFor(user).PutAsJsonAsync($"/groups/{groupId}/planned-start", new { plannedStartAt });

    private List<(string Type, JsonElement Data)> Events() =>
        Factory.WithDb(db => db.OutboxMessages.OrderBy(m => m.OccurredAt).ToList())
            .Select(m => (m.Type, JsonDocument.Parse(m.Payload).RootElement.Clone()))
            .ToList();

    private static List<Guid> MemberUsers(JsonElement data) =>
        data.GetProperty("members").EnumerateArray().Select(m => m.GetProperty("userId").GetGuid()).OrderBy(x => x).ToList();

    [IntegrationFact]
    public async Task Ready_group_with_date_is_confirmed_and_announced_with_members()
    {
        var (groupId, professor, students, _) = await ReadyGroup();

        await AssertStatus(HttpStatusCode.NoContent, await Schedule(professor, groupId, FirstLesson));

        var group = await GetGroup(professor, groupId);
        Assert.Equal(FirstLesson, group.GetProperty("plannedStartAt").GetDateTime().ToUniversalTime());
        Assert.NotEqual(JsonValueKind.Null, group.GetProperty("confirmedAt").ValueKind);

        var confirmed = Assert.Single(Events(), e => e.Type == "GroupConfirmed").Data;
        Assert.Equal(groupId, confirmed.GetProperty("groupId").GetGuid());
        Assert.Equal(CourseId, confirmed.GetProperty("courseId").GetGuid());
        Assert.Equal("Group", confirmed.GetProperty("groupType").GetString());
        Assert.Equal(FirstLesson, confirmed.GetProperty("firstLessonAt").GetDateTime().ToUniversalTime());
        Assert.Equal(students.Select(s => s.Id).OrderBy(x => x), MemberUsers(confirmed));
    }

    [IntegrationFact]
    public async Task Date_before_enough_members_confirms_when_the_group_fills_up()
    {
        var professor = TestUser.Professor();
        var groupId = await CreateGroup(professor);
        await AddMember(professor, groupId, TestUser.Student());
        await AssertStatus(HttpStatusCode.NoContent, await Schedule(professor, groupId, FirstLesson));
        Assert.DoesNotContain(Events(), e => e.Type == "GroupConfirmed");

        await AddMember(professor, groupId, TestUser.Student());
        await AddMember(professor, groupId, TestUser.Student());

        Assert.Single(Events(), e => e.Type == "GroupConfirmed");
    }

    [IntegrationFact]
    public async Task Late_member_of_confirmed_group_is_announced_alone()
    {
        var (groupId, professor, _, _) = await ReadyGroup();
        await Schedule(professor, groupId, FirstLesson);
        var late = TestUser.Student();

        var enrollmentId = await AddMember(professor, groupId, late);

        var joined = Assert.Single(Events(), e => e.Type == "MemberJoinedConfirmedGroup").Data;
        Assert.Equal(late.Id, joined.GetProperty("member").GetProperty("userId").GetGuid());
        Assert.Equal(enrollmentId, joined.GetProperty("member").GetProperty("enrollmentId").GetGuid());
        Assert.Equal(FirstLesson, joined.GetProperty("firstLessonAt").GetDateTime().ToUniversalTime());
        Assert.Single(Events(), e => e.Type == "GroupConfirmed");
    }

    [IntegrationFact]
    public async Task Moving_the_date_is_announced_and_same_date_is_ignored()
    {
        var (groupId, professor, students, _) = await ReadyGroup();
        await Schedule(professor, groupId, FirstLesson);
        var moved = FirstLesson.AddDays(7);

        await AssertStatus(HttpStatusCode.NoContent, await Schedule(professor, groupId, moved));
        await AssertStatus(HttpStatusCode.NoContent, await Schedule(professor, groupId, moved));

        var rescheduled = Assert.Single(Events(), e => e.Type == "FirstLessonRescheduled").Data;
        Assert.Equal(FirstLesson, rescheduled.GetProperty("oldFirstLessonAt").GetDateTime().ToUniversalTime());
        Assert.Equal(moved, rescheduled.GetProperty("newFirstLessonAt").GetDateTime().ToUniversalTime());
        Assert.Equal(students.Select(s => s.Id).OrderBy(x => x), MemberUsers(rescheduled));
        Assert.Single(Events(), e => e.Type == "GroupConfirmed");
    }

    [IntegrationFact]
    public async Task Moving_the_date_before_confirmation_sends_no_event()
    {
        var professor = TestUser.Professor();
        var groupId = await CreateGroup(professor);
        await AddMember(professor, groupId, TestUser.Student());
        await Schedule(professor, groupId, FirstLesson);

        await AssertStatus(HttpStatusCode.NoContent, await Schedule(professor, groupId, FirstLesson.AddDays(3)));

        Assert.DoesNotContain(Events(), e => e.Type is "FirstLessonRescheduled" or "GroupConfirmed");
    }

    [IntegrationFact]
    public async Task Date_with_time_zone_is_stored_as_utc()
    {
        var (groupId, professor, _, _) = await ReadyGroup();

        var day = DateTime.UtcNow.Date.AddDays(60);
        var local = $"{day:yyyy-MM-dd}T18:00:00+01:00";

        await AssertStatus(HttpStatusCode.NoContent, await Schedule(professor, groupId, local));

        var stored = Factory.WithDb(db => db.Groups.Single(g => g.Id == groupId).PlannedStartAt);
        Assert.Equal(DateTime.SpecifyKind(day.AddHours(17), DateTimeKind.Utc), stored);
    }

    [IntegrationFact]
    public async Task Changing_the_date_is_written_to_the_audit_log()
    {
        var (groupId, professor, _, _) = await ReadyGroup();
        await Schedule(professor, groupId, FirstLesson);

        var audit = await Json(await Get(TestUser.Admin(), $"/audit?entityId={groupId}"));

        Assert.Contains(audit.EnumerateArray(), a =>
            a.GetProperty("action").GetString() == "SchedulePlannedStart"
            && a.GetProperty("actorUserId").GetGuid() == professor.Id);
    }

    // ---------- odbijeni zahtevi ----------

    [IntegrationFact]
    public async Task Past_or_too_far_dates_are_rejected_without_events()
    {
        var (groupId, professor, _, _) = await ReadyGroup();

        await AssertProblem(HttpStatusCode.BadRequest, "CANNOT_SCHEDULE",
            await Schedule(professor, groupId, DateTime.UtcNow.AddMinutes(-5)));
        await AssertProblem(HttpStatusCode.BadRequest, "CANNOT_SCHEDULE",
            await Schedule(professor, groupId, DateTime.UtcNow.AddDays(400)));

        Assert.DoesNotContain(Events(), e => e.Type == "GroupConfirmed");
        Assert.Equal(JsonValueKind.Null, (await GetGroup(professor, groupId)).GetProperty("plannedStartAt").ValueKind);
    }

    [IntegrationFact]
    public async Task Missing_or_unreadable_date_is_rejected()
    {
        var (groupId, professor, _, _) = await ReadyGroup();

        var missing = await ClientFor(professor).PutAsJsonAsync($"/groups/{groupId}/planned-start", new { });
        var garbage = await Schedule(professor, groupId, "next tuesday");

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, garbage.StatusCode);
    }

    [IntegrationFact]
    public async Task Started_group_cannot_get_a_planned_date()
    {
        var (groupId, professor, _, _) = await ReadyGroup();
        await AssertStatus(HttpStatusCode.NoContent, await Post(professor, $"/groups/{groupId}/start"));

        await AssertProblem(HttpStatusCode.BadRequest, "CANNOT_SCHEDULE", await Schedule(professor, groupId, FirstLesson));
    }

    [IntegrationFact]
    public async Task Students_and_anonymous_users_cannot_set_the_date()
    {
        var (groupId, _, students, _) = await ReadyGroup();

        Assert.Equal(HttpStatusCode.Forbidden, (await Schedule(students[0], groupId, FirstLesson)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Schedule(null, groupId, FirstLesson)).StatusCode);
        Assert.DoesNotContain(Events(), e => e.Type == "GroupConfirmed");
    }

    [IntegrationFact]
    public async Task Unknown_group_returns_not_found()
    {
        var response = await Schedule(TestUser.Professor(), Guid.NewGuid(), FirstLesson);

        await AssertStatus(HttpStatusCode.NotFound, response);
    }
}
