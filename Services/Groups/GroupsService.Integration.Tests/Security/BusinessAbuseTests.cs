using System.Net;
using GroupsService.Integration.Tests.Infrastructure;

namespace GroupsService.Integration.Tests.Security;

// Pokusaji da se zaobidju poslovna pravila poznatim id-jem: upad u tudju privatnu grupu,
// grupu drugog kursa ili zatvorenu grupu, ponavljanje vec zavrsenih akcija.
public class BusinessAbuseTests : ApiTestBase
{
    public BusinessAbuseTests(ApiFactory factory) : base(factory) { }

    private int MembersOf(Guid groupId) => Factory.WithDb(db => db.Groups
        .Where(g => g.Id == groupId).SelectMany(g => g.Members).Count());

    [IntegrationFact]
    public async Task A_student_cannot_join_someone_elses_individual_group_by_its_id()
    {
        var owner = TestUser.Student();
        var created = await Post(owner, "/enrollments/individual", new { courseId = CourseId, professorId = Guid.NewGuid() });
        await AssertStatus(HttpStatusCode.Created, created);
        var privateGroupId = (await Json(created)).GetProperty("groupId").GetGuid();

        var intruder = await Post(TestUser.Student(), "/enrollments/group", new { groupId = privateGroupId });

        Assert.Equal(HttpStatusCode.BadRequest, intruder.StatusCode);
        Assert.Equal(1, MembersOf(privateGroupId));
    }

    [IntegrationFact]
    public async Task Staff_cannot_add_a_third_person_to_a_pair()
    {
        var inviter = TestUser.Student();
        var invitee = TestUser.Student();
        var invite = await Post(inviter, "/enrollments/invite-partner", new { courseId = CourseId, inviteeUserId = invitee.Id });
        await AssertStatus(HttpStatusCode.Created, invite);
        var token = Factory.WithDb(db => db.PartnerInvitations.Single().Token);
        var accepted = await Post(null, $"/invitations/{token}/accept");
        await AssertStatus(HttpStatusCode.OK, accepted);
        var pairId = (await Json(accepted)).GetProperty("groupId").GetGuid();

        var added = await Post(TestUser.Admin(), $"/groups/{pairId}/members", new { userId = TestUser.Student().Id });

        Assert.Equal(HttpStatusCode.BadRequest, added.StatusCode);
        Assert.Equal(2, MembersOf(pairId));
    }

    [IntegrationFact]
    public async Task Nobody_can_join_a_started_or_completed_group()
    {
        var (groupId, professor, _, _) = await ReadyGroup();
        await AssertStatus(HttpStatusCode.NoContent, await Post(professor, $"/groups/{groupId}/start"));

        Assert.Equal(HttpStatusCode.BadRequest, (await Post(TestUser.Student(), "/enrollments/group", new { groupId })).StatusCode);

        await AssertStatus(HttpStatusCode.NoContent, await Post(TestUser.Admin(), $"/groups/{groupId}/complete"));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await Post(professor, $"/groups/{groupId}/members", new { userId = TestUser.Student().Id })).StatusCode);
        Assert.Equal(3, MembersOf(groupId));
    }

    [IntegrationFact]
    public async Task A_group_of_another_course_does_not_give_access_to_this_course()
    {
        var otherCourse = Guid.NewGuid();
        var professor = TestUser.Professor();
        var created = await Post(professor, "/groups", new { courseId = otherCourse, slots = Slots(professor.Id) });
        await AssertStatus(HttpStatusCode.Created, created);
        var groupId = (await Json(created)).GetProperty("groupId").GetGuid();
        var students = new[] { TestUser.Student(), TestUser.Student(), TestUser.Student() };
        foreach (var s in students) await AddMember(professor, groupId, s);
        await AssertStatus(HttpStatusCode.NoContent, await Post(professor, $"/groups/{groupId}/start"));

        var access = await Access(students[0]); // pita za CourseId, a upisan je na drugi kurs
        Assert.False(access.GetProperty("hasAccess").GetBoolean());
    }

    [IntegrationFact]
    public async Task Repeating_finished_actions_is_rejected_and_changes_nothing()
    {
        var (groupId, professor, students, ids) = await ReadyGroup();
        await AssertStatus(HttpStatusCode.NoContent, await Post(professor, $"/groups/{groupId}/start"));

        await AssertStatus(HttpStatusCode.NoContent, await Post(students[0], $"/enrollments/{ids[0]}/cancel"));
        await AssertProblem(HttpStatusCode.BadRequest, "CANNOT_CANCEL", await Post(students[0], $"/enrollments/{ids[0]}/cancel"));
        await AssertProblem(HttpStatusCode.BadRequest, "CANNOT_FREEZE", await Post(professor, $"/enrollments/{ids[0]}/freeze"));

        await AssertStatus(HttpStatusCode.NoContent, await Post(professor, $"/enrollments/{ids[1]}/freeze"));
        await AssertProblem(HttpStatusCode.BadRequest, "CANNOT_FREEZE", await Post(professor, $"/enrollments/{ids[1]}/freeze"));
        await AssertStatus(HttpStatusCode.NoContent, await Post(professor, $"/enrollments/{ids[1]}/unfreeze"));
        await AssertProblem(HttpStatusCode.BadRequest, "CANNOT_UNFREEZE", await Post(professor, $"/enrollments/{ids[1]}/unfreeze"));
    }

    [IntegrationFact]
    public async Task Students_cannot_freeze_or_unfreeze_even_their_own_enrollment()
    {
        var (groupId, professor, students, ids) = await ReadyGroup();
        await AssertStatus(HttpStatusCode.NoContent, await Post(professor, $"/groups/{groupId}/start"));

        Assert.Equal(HttpStatusCode.Forbidden, (await Post(students[0], $"/enrollments/{ids[0]}/freeze")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(students[0], $"/enrollments/{ids[0]}/unfreeze")).StatusCode);
    }

    [IntegrationFact]
    public async Task A_student_cannot_invite_themselves_through_a_second_account_trick_or_invite_twice_at_once()
    {
        var inviter = TestUser.Student();
        var invitee = TestUser.Student();
        await AssertStatus(HttpStatusCode.Created,
            await Post(inviter, "/enrollments/invite-partner", new { courseId = CourseId, inviteeUserId = invitee.Id }));

        // Isti poziv ponovo dok prvi ceka - ne sme da napravi drugi upis za pozivaoca.
        await Post(inviter, "/enrollments/invite-partner", new { courseId = CourseId, inviteeUserId = invitee.Id });
        Assert.Equal(1, Factory.WithDb(db => db.Enrollments.Count(e => e.UserId == inviter.Id)));
    }
}
