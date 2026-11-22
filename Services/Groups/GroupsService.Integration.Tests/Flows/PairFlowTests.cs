using System.Net;
using GroupsService.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GroupsService.Integration.Tests.Flows;

// Pair tip: poziv partneru, pregled i prihvatanje/odbijanje preko tokena (stranica fronta
// zove GET pregled pa POST accept/decline), pokusaji, rasformiranje kad partner otkaze i start.
public class PairFlowTests : ApiTestBase
{
    public PairFlowTests(ApiFactory factory) : base(factory) { }

    private async Task<(Guid enrollmentId, Guid invitationId, string token)> Invite(TestUser inviter, TestUser invitee)
    {
        var response = await Post(inviter, "/enrollments/invite-partner",
            new { courseId = CourseId, inviteeUserId = invitee.Id });
        await AssertStatus(HttpStatusCode.Created, response);
        var json = await Json(response);
        var invitationId = json.GetProperty("invitationId").GetGuid();
        var token = Factory.WithDb(db => db.PartnerInvitations.Single(i => i.Id == invitationId).Token);
        return (json.GetProperty("enrollmentId").GetGuid(), invitationId, token);
    }

    // Rute pozivnica ne traze JWT - token iz linka je dovoljan.
    private Task<HttpResponseMessage> AcceptInvitation(string token) => Post(null, $"/invitations/{token}/accept");

    private Task<HttpResponseMessage> DeclineInvitation(string token) => Post(null, $"/invitations/{token}/decline");

    private async Task<Guid> GroupIdOf(TestUser student)
    {
        var mine = await MyEnrollments(student);
        return mine.EnumerateArray().First(e => e.GetProperty("status").GetString() != "Cancelled")
            .GetProperty("groupId").GetGuid();
    }

    // Spojen par (oba upisa Ready u istoj Pair grupi).
    private async Task<(TestUser inviter, TestUser invitee, Guid groupId, Guid inviterEnrollment, Guid inviteeEnrollment)> Pair()
    {
        var inviter = TestUser.Student();
        var invitee = TestUser.Student();
        var (inviterEnrollment, _, token) = await Invite(inviter, invitee);
        var accepted = await AcceptInvitation(token);
        await AssertStatus(HttpStatusCode.OK, accepted);
        var json = await Json(accepted);
        return (inviter, invitee, json.GetProperty("groupId").GetGuid(), inviterEnrollment,
            json.GetProperty("enrollmentId").GetGuid());
    }

    // ---------- poziv ----------

    [IntegrationFact]
    public async Task Invitation_preview_shows_the_course_and_inviter_and_tolerates_missing_data()
    {
        var inviter = TestUser.Student();
        var invitee = TestUser.Student();
        Factory.Users.Add(inviter.Id, "Marko Test");
        var (_, _, token) = await Invite(inviter, invitee);

        var preview = await Json(await Get(null, $"/invitations/{token}"));

        Assert.Equal("Pending", preview.GetProperty("status").GetString());
        Assert.False(preview.GetProperty("isExpired").GetBoolean());
        Assert.Equal("Spanish A1", preview.GetProperty("courseName").GetString());
        Assert.Equal("Marko Test", preview.GetProperty("inviterName").GetString());

        // Auth/Courses ne znaju podatke: polja su null, ne greska
        Factory.Users.Clear();
        Factory.Courses.Clear();
        var degraded = await Json(await Get(null, $"/invitations/{token}"));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, degraded.GetProperty("courseName").ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, degraded.GetProperty("inviterName").ValueKind);
    }

    [IntegrationFact]
    public async Task Invite_validates_self_invites_and_people_who_are_already_enrolled()
    {
        var inviter = TestUser.Student();
        var self = await Post(inviter, "/enrollments/invite-partner", new { courseId = CourseId, inviteeUserId = inviter.Id });
        await AssertStatus(HttpStatusCode.BadRequest, self);
        Assert.Equal("INVALID_INVITATION", await ErrorCode(self));

        // pozvani vec ima upis na kurs
        var professor = TestUser.Professor();
        var invitee = TestUser.Student();
        await AddMember(professor, await CreateGroup(professor), invitee);
        await AssertStatus(HttpStatusCode.BadRequest,
            await Post(inviter, "/enrollments/invite-partner", new { courseId = CourseId, inviteeUserId = invitee.Id }));

        // inicijator vec ima upis (Group tip) pa ne moze da pravi par
        var member = TestUser.Student();
        await AddMember(professor, await CreateGroup(professor, hour: 9), member);
        await AssertStatus(HttpStatusCode.BadRequest,
            await Post(member, "/enrollments/invite-partner", new { courseId = CourseId, inviteeUserId = Guid.NewGuid() }));
    }

    // ---------- prihvatanje ----------

    [IntegrationFact]
    public async Task Accepting_forms_a_Ready_pair_and_the_professor_must_assign_slots_before_start()
    {
        var professor = TestUser.Professor();
        var (inviter, invitee, groupId, inviterEnrollment, inviteeEnrollment) = await Pair();

        Assert.Equal("Ready", await EnrollmentStatus(inviter, inviterEnrollment));
        Assert.Equal("Ready", await EnrollmentStatus(invitee, inviteeEnrollment));
        var group = await GetGroup(professor, groupId);
        Assert.Equal("Pair", group.GetProperty("type").GetString());
        Assert.Equal("Ready", group.GetProperty("status").GetString());
        Assert.Equal(2, group.GetProperty("members").GetArrayLength());

        // termini jos nemaju profesora
        var early = await Post(professor, $"/groups/{groupId}/start");
        await AssertStatus(HttpStatusCode.BadRequest, early);
        Assert.Equal("CANNOT_START", await ErrorCode(early));

        var days = new[] { "Monday", "Wednesday", "Friday" };
        for (var i = 0; i < 3; i++)
            await AssertStatus(HttpStatusCode.NoContent, await Post(professor, $"/groups/{groupId}/assign-professor-slot",
                new { slotIndex = i, dayOfWeek = days[i], time = "18:00:00", professorId = professor.Id }));

        await AssertStatus(HttpStatusCode.NoContent, await Post(professor, $"/groups/{groupId}/start"));
        Assert.Equal("Active", await EnrollmentStatus(inviter, inviterEnrollment));
        Assert.True((await Access(invitee)).GetProperty("hasAccess").GetBoolean());
    }

    [IntegrationFact]
    public async Task Accept_returns_the_new_enrollment_and_group_and_only_works_once()
    {
        var inviter = TestUser.Student();
        var invitee = TestUser.Student();
        var (_, _, token) = await Invite(inviter, invitee);

        var accepted = await AcceptInvitation(token);
        await AssertStatus(HttpStatusCode.OK, accepted);
        var json = await Json(accepted);
        Assert.Equal(json.GetProperty("groupId").GetGuid(), await GroupIdOf(invitee));

        await AssertProblem(HttpStatusCode.BadRequest, "CANNOT_ACCEPT_INVITATION", await AcceptInvitation(token));
        Assert.Equal(1, Factory.WithDb(db => db.Groups.Count()));
    }

    [IntegrationFact]
    public async Task Preview_is_read_only_and_does_not_accept_the_invitation()
    {
        var inviter = TestUser.Student();
        var (_, _, token) = await Invite(inviter, TestUser.Student());

        // npr. skener linkova u mejl klijentu otvori link vise puta
        await Get(null, $"/invitations/{token}");
        await Get(null, $"/invitations/{token}");

        Assert.Equal(0, Factory.WithDb(db => db.Groups.Count()));
        Assert.Equal("Pending", (await Json(await Get(null, $"/invitations/{token}"))).GetProperty("status").GetString());
    }

    [IntegrationFact]
    public async Task Unknown_or_expired_tokens_cannot_be_accepted_or_declined()
    {
        await AssertProblem(HttpStatusCode.NotFound, "INVITATION_NOT_FOUND", await AcceptInvitation("nope"));
        await AssertProblem(HttpStatusCode.NotFound, "INVITATION_NOT_FOUND", await DeclineInvitation("nope"));

        var inviter = TestUser.Student();
        var invitee = TestUser.Student();
        var (_, invitationId, token) = await Invite(inviter, invitee);
        Factory.WithDb(db => db.Database.ExecuteSqlRaw(
            "UPDATE partner_invitations SET expires_at = now() - interval '1 day' WHERE id = {0}", invitationId));

        await AssertProblem(HttpStatusCode.BadRequest, "CANNOT_ACCEPT_INVITATION", await AcceptInvitation(token));
        Assert.Equal(0, Factory.WithDb(db => db.Groups.Count()));
    }

    [IntegrationFact]
    public async Task Second_accept_from_a_double_invitation_is_rejected_clearly()
    {
        var inviter = TestUser.Student();
        var (_, _, first) = await Invite(inviter, TestUser.Student());
        var (_, _, second) = await Invite(inviter, TestUser.Student());

        await AssertStatus(HttpStatusCode.OK, await AcceptInvitation(first));
        var rejected = await AcceptInvitation(second);

        await AssertProblem(HttpStatusCode.BadRequest, "CANNOT_ACCEPT_INVITATION", rejected);
        Assert.Contains("no longer looking for a partner", await ErrorDetail(rejected));
        Assert.Equal(1, Factory.WithDb(db => db.Groups.Count()));
    }

    // ---------- odbijanje i pokusaji ----------

    [IntegrationFact]
    public async Task Declined_invitations_count_as_attempts_up_to_the_maximum()
    {
        var inviter = TestUser.Student();
        var invitee = TestUser.Student();

        var (_, _, first) = await Invite(inviter, invitee);
        await AssertStatus(HttpStatusCode.NoContent, await DeclineInvitation(first));
        var status = await Json(await Get(null, $"/invitations/{first}"));
        Assert.Equal("Declined", status.GetProperty("status").GetString());

        var (_, _, second) = await Invite(inviter, invitee); // pokusaj 2
        await AssertStatus(HttpStatusCode.NoContent, await DeclineInvitation(second));

        var third = await Post(inviter, "/enrollments/invite-partner",
            new { courseId = CourseId, inviteeUserId = invitee.Id });
        await AssertStatus(HttpStatusCode.BadRequest, third);
        Assert.Contains("Maximum number of invitation attempts", await ErrorDetail(third));

        // drugi partner je dozvoljen, i dalje je isti upis inicijatora
        await Invite(inviter, TestUser.Student());
        Assert.Single(Factory.WithDb(db => db.Enrollments.Where(e => e.UserId == inviter.Id).ToList()));
    }

    // ---------- otkazivanje ----------

    [IntegrationFact]
    public async Task Partner_cancelling_before_start_dissolves_the_pair_and_frees_the_other_one()
    {
        var professor = TestUser.Professor();
        var (inviter, invitee, groupId, inviterEnrollment, inviteeEnrollment) = await Pair();

        await AssertStatus(HttpStatusCode.NoContent, await Post(invitee, $"/enrollments/{inviteeEnrollment}/cancel"));

        Assert.Equal("Dissolved", (await GetGroup(professor, groupId)).GetProperty("status").GetString());
        Assert.Equal("Searching", await EnrollmentStatus(inviter, inviterEnrollment));
        var mine = await MyEnrollments(inviter);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, mine[0].GetProperty("groupId").ValueKind);

        // preostali moze da pozove novog partnera i istog (prihvacen poziv se ne racuna kao pokusaj)
        await Invite(inviter, TestUser.Student());
        var (_, _, again) = await Invite(inviter, invitee);
        await AssertStatus(HttpStatusCode.OK, await AcceptInvitation(again));
    }

    [IntegrationFact]
    public async Task Inviter_cancelling_before_start_frees_the_invitee()
    {
        var professor = TestUser.Professor();
        var (inviter, invitee, groupId, inviterEnrollment, inviteeEnrollment) = await Pair();

        await AssertStatus(HttpStatusCode.NoContent, await Post(inviter, $"/enrollments/{inviterEnrollment}/cancel"));

        Assert.Equal("Dissolved", (await GetGroup(professor, groupId)).GetProperty("status").GetString());
        Assert.Equal("Searching", await EnrollmentStatus(invitee, inviteeEnrollment));
    }

    [IntegrationFact]
    public async Task A_dissolved_pair_does_not_hold_the_professors_slots()
    {
        var professor = TestUser.Professor();
        var (inviter, invitee, groupId, _, inviteeEnrollment) = await Pair();
        await Post(professor, $"/groups/{groupId}/assign-professor-slot",
            new { slotIndex = 0, dayOfWeek = "Monday", time = "18:00:00", professorId = professor.Id });
        await Post(invitee, $"/enrollments/{inviteeEnrollment}/cancel");

        // isto vreme profesor sada moze da dobije u novoj grupi
        await CreateGroup(professor, hour: 18);
    }

    [IntegrationFact]
    public async Task Cancelling_one_member_of_an_active_pair_keeps_the_group_active()
    {
        var professor = TestUser.Professor();
        var (inviter, invitee, groupId, inviterEnrollment, inviteeEnrollment) = await Pair();
        var days = new[] { "Monday", "Wednesday", "Friday" };
        for (var i = 0; i < 3; i++)
            await Post(professor, $"/groups/{groupId}/assign-professor-slot",
                new { slotIndex = i, dayOfWeek = days[i], time = "18:00:00", professorId = professor.Id });
        await Post(professor, $"/groups/{groupId}/start");

        await AssertStatus(HttpStatusCode.NoContent, await Post(invitee, $"/enrollments/{inviteeEnrollment}/cancel"));

        Assert.Equal("Active", (await GetGroup(professor, groupId)).GetProperty("status").GetString());
        Assert.Equal("Active", await EnrollmentStatus(inviter, inviterEnrollment));
    }
}