using System.Net;
using System.Text;
using GroupsService.Integration.Tests.Infrastructure;

namespace GroupsService.Integration.Tests.Security;

// Sta korisnik (ili los klijent) moze da posalje: prazni id-jevi, nepostojece vrednosti,
// ekstremni brojevi i datumi. Pravilo: odgovor je uvek 400 sa kodom - nikad 500 i nikad
// upis besmislenih podataka.
public class InputValidationTests : ApiTestBase
{
    public InputValidationTests(ApiFactory factory) : base(factory) { }

    private static async Task AssertBadRequest(HttpResponseMessage response)
    {
        await AssertStatus(HttpStatusCode.BadRequest, response);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.False(string.IsNullOrWhiteSpace(await ErrorCode(response)));
    }

    private Task<HttpResponseMessage> PostRaw(TestUser user, string url, string json) =>
        ClientFor(user).PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));

    private string SlotsJson(Guid professorId, string day = "\"Monday\"", string time = "\"18:00:00\"") =>
        $$"""[{"dayOfWeek":{{day}},"time":{{time}},"professorId":"{{professorId}}"},{"dayOfWeek":"Wednesday","time":"18:00:00","professorId":"{{professorId}}"},{"dayOfWeek":"Friday","time":"18:00:00","professorId":"{{professorId}}"}]""";

    private int GroupCount() => Factory.WithDb(db => db.Groups.Count());

    // ---------- kreiranje grupe ----------

    [IntegrationFact]
    public async Task Create_group_rejects_an_empty_course_id()
    {
        var professor = TestUser.Professor();
        await AssertBadRequest(await Post(professor, "/groups", new { courseId = Guid.Empty, slots = Slots(professor.Id) }));
        Assert.Equal(0, GroupCount());
    }

    [IntegrationFact]
    public async Task Create_group_rejects_an_empty_professor_in_a_slot()
    {
        var professor = TestUser.Professor();
        await AssertBadRequest(await Post(professor, "/groups", new { courseId = CourseId, slots = Slots(Guid.Empty) }));
        Assert.Equal(0, GroupCount());
    }

    [IntegrationTheory]
    [InlineData("\"Funday\"")]
    [InlineData("9")]
    [InlineData("-1")]
    [InlineData("null")]
    public async Task Create_group_rejects_a_day_that_does_not_exist(string day)
    {
        var professor = TestUser.Professor();
        var response = await PostRaw(professor, "/groups",
            $$"""{"courseId":"{{CourseId}}","slots":{{SlotsJson(professor.Id, day: day)}}}""");
        await AssertBadRequest(response);
        Assert.Equal(0, GroupCount());
    }

    [IntegrationTheory]
    [InlineData("\"25:00:00\"")]
    [InlineData("\"noon\"")]
    [InlineData("null")]
    public async Task Create_group_rejects_a_time_that_does_not_exist(string time)
    {
        var professor = TestUser.Professor();
        var response = await PostRaw(professor, "/groups",
            $$"""{"courseId":"{{CourseId}}","slots":{{SlotsJson(professor.Id, time: time)}}}""");
        await AssertBadRequest(response);
        Assert.Equal(0, GroupCount());
    }

    [IntegrationTheory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(366)]
    [InlineData(int.MaxValue)]
    public async Task Create_group_rejects_an_unreasonable_formation_window(int days)
    {
        var professor = TestUser.Professor();
        await AssertBadRequest(await Post(professor, "/groups",
            new { courseId = CourseId, slots = Slots(professor.Id), formationWindowDays = days }));
        Assert.Equal(0, GroupCount());
    }

    [IntegrationTheory]
    [InlineData("""{"courseId":null,"slots":[]}""")]
    [InlineData("""{"slots":null}""")]
    [InlineData("""[]""")]
    [InlineData("""{}""")]
    public async Task Create_group_rejects_missing_or_wrong_shaped_bodies(string json)
    {
        await AssertBadRequest(await PostRaw(TestUser.Professor(), "/groups", json));
        Assert.Equal(0, GroupCount());
    }

    [IntegrationFact]
    public async Task Create_group_rejects_more_slots_than_allowed()
    {
        var professor = TestUser.Professor();
        var slots = Enumerable.Range(0, 50)
            .Select(i => new { dayOfWeek = "Monday", time = $"{i % 24:00}:{i % 60:00}:00", professorId = professor.Id });
        await AssertBadRequest(await Post(professor, "/groups", new { courseId = CourseId, slots }));
        Assert.Equal(0, GroupCount());
    }

    [IntegrationFact]
    public async Task A_body_that_is_not_json_is_rejected_without_a_server_error()
    {
        var response = await ClientFor(TestUser.Professor()).PostAsync("/groups",
            new StringContent("courseId=1", Encoding.UTF8, "application/x-www-form-urlencoded"));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    // ---------- clanovi i upisi ----------

    [IntegrationFact]
    public async Task Adding_a_member_rejects_an_empty_user_id()
    {
        var professor = TestUser.Professor();
        var groupId = await CreateGroup(professor);
        await AssertBadRequest(await Post(professor, $"/groups/{groupId}/members", new { userId = Guid.Empty }));
        Assert.Equal(0, Factory.WithDb(db => db.Enrollments.Count()));
    }

    [IntegrationFact]
    public async Task Individual_enrollment_rejects_empty_ids()
    {
        var student = TestUser.Student();
        await AssertBadRequest(await Post(student, "/enrollments/individual", new { courseId = Guid.Empty, professorId = Guid.NewGuid() }));
        await AssertBadRequest(await Post(student, "/enrollments/individual", new { courseId = CourseId, professorId = Guid.Empty }));
        Assert.Equal(0, Factory.WithDb(db => db.Enrollments.Count()));
    }

    [IntegrationFact]
    public async Task Joining_with_an_empty_group_id_is_just_not_found()
    {
        await AssertProblem(HttpStatusCode.NotFound, "GROUP_NOT_FOUND",
            await Post(TestUser.Student(), "/enrollments/group", new { groupId = Guid.Empty }));
    }

    [IntegrationFact]
    public async Task Inviting_rejects_empty_ids()
    {
        var student = TestUser.Student();
        await AssertBadRequest(await Post(student, "/enrollments/invite-partner", new { courseId = CourseId, inviteeUserId = Guid.Empty }));
        await AssertBadRequest(await Post(student, "/enrollments/invite-partner", new { courseId = Guid.Empty, inviteeUserId = Guid.NewGuid() }));
        Assert.Equal(0, Factory.WithDb(db => db.Enrollments.Count()));
    }

    [IntegrationFact]
    public async Task Access_check_rejects_an_empty_course_id()
    {
        await AssertBadRequest(await Get(TestUser.Student(), $"/enrollments/access?courseId={Guid.Empty}"));
    }

    // ---------- start i termini ----------

    [IntegrationTheory]
    [InlineData("0001-01-01T00:00:00Z")]
    [InlineData("9999-12-31T23:59:59Z")]
    [InlineData("2000-01-01T00:00:00Z")]
    public async Task Start_rejects_an_unreasonable_unlock_date(string unlockStartsAt)
    {
        var (groupId, professor, _, _) = await ReadyGroup();
        var response = await PostRaw(professor, $"/groups/{groupId}/start", $$"""{"unlockStartsAt":"{{unlockStartsAt}}"}""");
        await AssertBadRequest(response);
        Assert.Equal("Ready", (await GetGroup(professor, groupId)).GetProperty("status").GetString());
    }

    [IntegrationFact]
    public async Task Assigning_a_slot_rejects_an_empty_professor_and_a_day_that_does_not_exist()
    {
        var professor = TestUser.Professor();
        var groupId = await CreateGroup(professor);
        await AssertBadRequest(await Post(professor, $"/groups/{groupId}/assign-professor-slot",
            new { slotIndex = 0, dayOfWeek = "Monday", time = "18:00:00", professorId = Guid.Empty }));
        // admin, jer samo on sme da menja vec dodeljen termin - inace bi 400 dosao iz tog pravila
        await AssertBadRequest(await PostRaw(TestUser.Admin(), $"/groups/{groupId}/assign-professor-slot",
            $$"""{"slotIndex":0,"dayOfWeek":12,"time":"18:00:00","professorId":"{{professor.Id}}"}"""));
    }

    // ---------- upiti ----------

    [IntegrationTheory]
    [InlineData("/groups?status=Bogus")]
    [InlineData("/groups?type=99")]
    [InlineData("/groups?courseId=not-a-guid")]
    public async Task List_filters_with_values_that_do_not_exist_are_rejected(string url)
    {
        await AssertBadRequest(await Get(TestUser.Admin(), url));
    }

    [IntegrationTheory]
    [InlineData(-5)]
    [InlineData(1_000_000)]
    public async Task Audit_page_size_is_clamped(int take)
    {
        var response = await Get(TestUser.Admin(), $"/audit?take={take}");
        await AssertStatus(HttpStatusCode.OK, response);
    }

    // ---------- pozivnice ----------

    [IntegrationTheory]
    [InlineData("' OR 1=1 --")]
    [InlineData("%00")]
    [InlineData("../../etc/passwd")]
    public async Task Strange_invitation_tokens_are_just_not_found(string token)
    {
        var escaped = Uri.EscapeDataString(token);
        await AssertProblem(HttpStatusCode.NotFound, "INVITATION_NOT_FOUND", await Get(null, $"/invitations/{escaped}"));
        await AssertProblem(HttpStatusCode.NotFound, "INVITATION_NOT_FOUND", await Post(null, $"/invitations/{escaped}/accept"));
    }

    [IntegrationFact]
    public async Task A_very_long_invitation_token_is_rejected_without_a_server_error()
    {
        var response = await Get(null, $"/invitations/{new string('a', 5000)}");
        Assert.True((int)response.StatusCode is 400 or 404 or 414, $"Got {(int)response.StatusCode}.");
    }
}
