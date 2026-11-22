using System.Net;
using GroupsService.Application.Commands.CourseCompleted;
using GroupsService.Domain.Exceptions;
using GroupsService.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace GroupsService.Integration.Tests.Persistence;

// Trke izmedju paralelnih zahteva nad pravom bazom. Pravila cuva baza (version/xmin
// token, jedinstveni indeks, advisory lock po profesoru), ne kod, a klijent koji izgubi
// trku dobija 400 ili 409, nikad 500.
public class ConcurrencyTests : ApiTestBase
{
    public ConcurrencyTests(ApiFactory factory) : base(factory) { }

    [IntegrationFact]
    public async Task Only_one_of_many_students_gets_the_last_seat()
    {
        var professor = TestUser.Professor();
        var groupId = await CreateGroup(professor);
        for (var i = 0; i < 5; i++) await AddMember(professor, groupId, TestUser.Student()); // 5 od 6

        var contenders = Enumerable.Range(0, 8).Select(_ => TestUser.Student()).ToList();
        var responses = await Task.WhenAll(contenders.Select(s => Post(s, "/enrollments/group", new { groupId })));

        var statuses = responses.Select(r => r.StatusCode).ToList();
        Assert.Equal(1, statuses.Count(s => s == HttpStatusCode.Created));
        // gubitnici dobijaju poslovnu gresku: 400 (grupa puna) ili 409 (izgubili trku)
        Assert.All(statuses.Where(s => s != HttpStatusCode.Created),
            s => Assert.Contains(s, new[] { HttpStatusCode.BadRequest, HttpStatusCode.Conflict }));
        Assert.Equal(6, (await GetGroup(professor, groupId)).GetProperty("members").GetArrayLength());
        Assert.Equal(6, Factory.WithDb(db => db.Enrollments.Count(e => e.GroupId == groupId)));
    }

    [IntegrationFact]
    public async Task Double_click_enrolling_the_same_student_creates_exactly_one_enrollment()
    {
        var professor = TestUser.Professor();
        var groupId = await CreateGroup(professor);
        var student = TestUser.Student();

        var responses = await Task.WhenAll(Enumerable.Range(0, 4)
            .Select(_ => Post(student, "/enrollments/group", new { groupId })));

        var statuses = responses.Select(r => r.StatusCode).ToList();
        Assert.Equal(1, statuses.Count(s => s == HttpStatusCode.Created));
        Assert.DoesNotContain(HttpStatusCode.InternalServerError, statuses);
        Assert.Equal(1, Factory.WithDb(db => db.Enrollments.Count(e => e.UserId == student.Id)));
    }

    [IntegrationFact]
    public async Task Parallel_individual_requests_from_the_same_student_create_one_enrollment()
    {
        var professor = TestUser.Professor();
        var student = TestUser.Student();

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            Post(student, "/enrollments/individual", new { courseId = CourseId, professorId = professor.Id })));

        var statuses = responses.Select(r => r.StatusCode).ToList();
        Assert.Equal(1, statuses.Count(s => s == HttpStatusCode.Created));
        Assert.DoesNotContain(HttpStatusCode.InternalServerError, statuses);
        Assert.Equal(1, Factory.WithDb(db => db.Enrollments.Count(e => e.UserId == student.Id)));
    }

    [IntegrationFact]
    public async Task Concurrent_starts_and_cancels_do_not_break_the_group()
    {
        var (groupId, professor, students, ids) = await ReadyGroup();

        var responses = await Task.WhenAll(
            Post(professor, $"/groups/{groupId}/start"),
            Post(students[0], $"/enrollments/{ids[0]}/cancel"),
            Post(students[1], $"/enrollments/{ids[1]}/cancel"));

        Assert.DoesNotContain(HttpStatusCode.InternalServerError, responses.Select(r => r.StatusCode));
        var group = await GetGroup(professor, groupId);
        var members = group.GetProperty("members").GetArrayLength();
        Assert.InRange(members, 0, 3);
    }

    // ---------- raspored profesora (advisory lock) ----------

    [IntegrationFact]
    public async Task Parallel_groups_cannot_double_book_the_same_professor()
    {
        var professor = TestUser.Professor();

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            Post(professor, "/groups", new { courseId = CourseId, slots = Slots(professor.Id, 9) })));

        var statuses = responses.Select(r => r.StatusCode).ToList();
        Assert.Equal(1, statuses.Count(s => s == HttpStatusCode.Created));
        foreach (var loser in responses.Where(r => r.StatusCode != HttpStatusCode.Created))
            await AssertProblem(HttpStatusCode.BadRequest, "CANNOT_CREATE_GROUP", loser);
        Assert.Equal(1, Factory.WithDb(db => db.Groups.Count()));
    }

    [IntegrationFact]
    public async Task Parallel_slot_assignments_in_two_groups_cannot_double_book_the_same_professor()
    {
        var admin = TestUser.Admin();
        var g1 = await CreateGroup(TestUser.Professor(), hour: 18);
        var g2 = await CreateGroup(TestUser.Professor(), hour: 18);
        var professorId = Guid.NewGuid();
        var assign = new { slotIndex = 0, dayOfWeek = "Tuesday", time = "10:00:00", professorId };

        var responses = await Task.WhenAll(
            Post(admin, $"/groups/{g1}/assign-professor-slot", assign),
            Post(admin, $"/groups/{g2}/assign-professor-slot", assign));

        var statuses = responses.Select(r => r.StatusCode).ToList();
        Assert.Equal(1, statuses.Count(s => s == HttpStatusCode.NoContent));
        await AssertProblem(HttpStatusCode.BadRequest, "CANNOT_ASSIGN",
            responses.Single(r => r.StatusCode != HttpStatusCode.NoContent));
    }

    // ---------- isti upis / isti poziv (xmin) ----------

    [IntegrationFact]
    public async Task Parallel_cancel_and_freeze_of_one_enrollment_leave_a_consistent_state()
    {
        var (groupId, professor, _, ids) = await ReadyGroup();
        await AssertStatus(HttpStatusCode.NoContent, await Post(professor, $"/groups/{groupId}/start"));

        var responses = await Task.WhenAll(
            Post(professor, $"/enrollments/{ids[0]}/cancel"),
            Post(professor, $"/enrollments/{ids[0]}/freeze"));

        var statuses = responses.Select(r => r.StatusCode).ToList();
        Assert.Contains(HttpStatusCode.NoContent, statuses);
        Assert.DoesNotContain(HttpStatusCode.InternalServerError, statuses);

        var (status, enrollmentGroupId) = Factory.WithDb(db =>
        {
            var e = db.Enrollments.Single(x => x.Id == ids[0]);
            return (e.Status.ToString(), e.GroupId);
        });
        var isMember = (await GetGroup(professor, groupId)).GetProperty("members").EnumerateArray()
            .Any(m => m.GetProperty("enrollmentId").GetGuid() == ids[0]);

        // otkazan upis nije u grupi; zamrznut (ili aktivan) jeste - nikad mesavina
        if (status == "Cancelled")
        {
            Assert.Null(enrollmentGroupId);
            Assert.False(isMember);
        }
        else
        {
            Assert.Equal(groupId, enrollmentGroupId);
            Assert.True(isMember);
        }
    }

    [IntegrationFact]
    public async Task Accepting_and_declining_the_same_invitation_at_once_cannot_both_succeed()
    {
        var inviter = TestUser.Student();
        var invite = await Post(inviter, "/enrollments/invite-partner",
            new { courseId = CourseId, inviteeUserId = TestUser.Student().Id });
        await AssertStatus(HttpStatusCode.Created, invite);
        var invitationId = (await Json(invite)).GetProperty("invitationId").GetGuid();
        var token = Factory.WithDb(db => db.PartnerInvitations.Single(i => i.Id == invitationId).Token);

        var responses = await Task.WhenAll(
            Post(null, $"/invitations/{token}/accept"),
            Post(null, $"/invitations/{token}/decline"));

        var accepted = responses[0].IsSuccessStatusCode;
        var declined = responses[1].IsSuccessStatusCode;
        Assert.True(accepted ^ declined, "Exactly one of accept/decline must win.");
        Assert.DoesNotContain(HttpStatusCode.InternalServerError, responses.Select(r => r.StatusCode));

        var (status, groups) = Factory.WithDb(db => (
            db.PartnerInvitations.Single(i => i.Id == invitationId).Status.ToString(),
            db.Groups.Count()));
        Assert.Equal(accepted ? "Accepted" : "Declined", status);
        Assert.Equal(accepted ? 1 : 0, groups);
    }

    // ---------- automatsko zatvaranje grupe (Version grupe) ----------

    [IntegrationFact]
    public async Task Last_two_members_finishing_at_once_still_close_the_group()
    {
        var (groupId, professor, students, ids) = await ReadyGroup();
        await AssertStatus(HttpStatusCode.NoContent, await Post(professor, $"/groups/{groupId}/start"));
        await CompleteWithRetry(ids[0], students[0]);

        await Task.WhenAll(
            CompleteWithRetry(ids[1], students[1]),
            CompleteWithRetry(ids[2], students[2]));

        Assert.Equal("Completed", (await GetGroup(professor, groupId)).GetProperty("status").GetString());
        foreach (var id in ids)
            Assert.Equal("Completed", Factory.WithDb(db => db.Enrollments.Single(e => e.Id == id).Status.ToString()));
    }

    // Isto sto ce raditi potrosac poruka: konflikt (izgubljena trka) -> poruka se ponavlja.
    private async Task CompleteWithRetry(Guid enrollmentId, TestUser student)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var scope = Factory.Services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<CourseCompletedHandler>()
                    .Handle(new CourseCompletedCommand(enrollmentId, student.Id, CourseId));
                return;
            }
            catch (ConcurrencyConflictException) when (attempt < 5)
            {
            }
        }
    }
}
