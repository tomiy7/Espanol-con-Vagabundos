using System.Net;
using GroupsService.Application.Commands.CourseCompleted;
using GroupsService.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace GroupsService.Integration.Tests.Flows;

// Courses javlja CourseCompleted (poruka); ovde se handler poziva direktno iz DI-ja jer
// potrosac poruka jos nije u repou. Provjerava se stanje upisa i grupe u pravoj bazi.
public class CourseCompletionTests : ApiTestBase
{
    public CourseCompletionTests(ApiFactory factory) : base(factory) { }

    private async Task CourseCompleted(Guid enrollmentId, TestUser student, Guid? courseId = null)
    {
        using var scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<CourseCompletedHandler>()
            .Handle(new CourseCompletedCommand(enrollmentId, student.Id, courseId ?? CourseId));
    }

    private async Task<(Guid groupId, TestUser professor, List<TestUser> students, List<Guid> ids)> ActiveGroup(int hour = 18)
    {
        var (groupId, professor, students, ids) = await ReadyGroup(hour);
        await AssertStatus(HttpStatusCode.NoContent, await Post(professor, $"/groups/{groupId}/start"));
        return (groupId, professor, students, ids);
    }

    [IntegrationFact]
    public async Task Completing_the_course_marks_the_enrollment_Completed_and_removes_access()
    {
        var (_, _, students, ids) = await ActiveGroup();

        await CourseCompleted(ids[0], students[0]);

        Assert.Equal("Completed", await EnrollmentStatus(students[0], ids[0]));
        var mine = await MyEnrollments(students[0]);
        Assert.NotEqual(System.Text.Json.JsonValueKind.Null, mine[0].GetProperty("completedAt").ValueKind);
        Assert.False((await Access(students[0])).GetProperty("hasAccess").GetBoolean());
        Assert.True((await Access(students[1])).GetProperty("hasAccess").GetBoolean());
    }

    [IntegrationFact]
    public async Task Group_closes_when_every_remaining_member_finished()
    {
        var (groupId, professor, students, ids) = await ActiveGroup();

        await CourseCompleted(ids[0], students[0]);
        await CourseCompleted(ids[1], students[1]);
        Assert.Equal("Active", (await GetGroup(professor, groupId)).GetProperty("status").GetString());

        await CourseCompleted(ids[2], students[2]);

        Assert.Equal("Completed", (await GetGroup(professor, groupId)).GetProperty("status").GetString());
    }

    [IntegrationFact]
    public async Task Frozen_and_cancelled_members_do_not_hold_the_group_open()
    {
        var (groupId, professor, students, ids) = await ActiveGroup();
        await Post(professor, $"/enrollments/{ids[2]}/freeze");
        await Post(students[1], $"/enrollments/{ids[1]}/cancel");

        await CourseCompleted(ids[0], students[0]);

        Assert.Equal("Completed", (await GetGroup(professor, groupId)).GetProperty("status").GetString());
        Assert.Equal("Frozen", await EnrollmentStatus(students[2], ids[2]));
    }

    [IntegrationFact]
    public async Task Duplicate_and_invalid_course_completed_messages_are_ignored()
    {
        var (groupId, professor, students, ids) = await ActiveGroup();
        await CourseCompleted(ids[0], students[0]);
        var completedAt = (await MyEnrollments(students[0]))[0].GetProperty("completedAt").GetString();

        await CourseCompleted(ids[0], students[0]);                               // duplikat
        await CourseCompleted(ids[1], students[0]);                               // tudj korisnik
        await CourseCompleted(ids[1], students[1], courseId: Guid.NewGuid());     // pogresan kurs
        await CourseCompleted(Guid.NewGuid(), students[1]);                       // nepoznat upis

        Assert.Equal(completedAt, (await MyEnrollments(students[0]))[0].GetProperty("completedAt").GetString());
        Assert.Equal("Active", await EnrollmentStatus(students[1], ids[1]));
        Assert.Equal("Active", (await GetGroup(professor, groupId)).GetProperty("status").GetString());
    }

    [IntegrationFact]
    public async Task Not_yet_active_and_frozen_enrollments_are_not_completed()
    {
        var (groupId, professor, students, ids) = await ReadyGroup();

        await CourseCompleted(ids[0], students[0]); // Ready, jos nije poceo
        Assert.Equal("Ready", await EnrollmentStatus(students[0], ids[0]));

        await Post(professor, $"/groups/{groupId}/start");
        await Post(professor, $"/enrollments/{ids[0]}/freeze");
        await CourseCompleted(ids[0], students[0]); // zamrznut
        Assert.Equal("Frozen", await EnrollmentStatus(students[0], ids[0]));
    }

    [IntegrationFact]
    public async Task A_completed_course_cannot_be_taken_again_but_a_cancelled_one_can()
    {
        var (_, professor, students, ids) = await ActiveGroup(hour: 18);
        await CourseCompleted(ids[0], students[0]);
        var another = await CreateGroup(professor, hour: 19);

        var join = await Post(students[0], "/enrollments/group", new { groupId = another });
        await AssertStatus(HttpStatusCode.BadRequest, join);
        Assert.Contains("already completed", await ErrorDetail(join));

        var individual = await Post(students[0], "/enrollments/individual", new { courseId = CourseId, professorId = professor.Id });
        await AssertStatus(HttpStatusCode.BadRequest, individual);

        var invite = await Post(students[0], "/enrollments/invite-partner", new { courseId = CourseId, inviteeUserId = Guid.NewGuid() });
        await AssertStatus(HttpStatusCode.BadRequest, invite);

        // otkazan upis se moze ponoviti kao NOV upis, bez napretka
        await Post(students[1], $"/enrollments/{ids[1]}/cancel");
        await AssertStatus(HttpStatusCode.Created, await Post(students[1], "/enrollments/group", new { groupId = another }));
    }
}