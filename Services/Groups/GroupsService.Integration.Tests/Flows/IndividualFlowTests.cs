using System.Net;
using GroupsService.Integration.Tests.Infrastructure;

namespace GroupsService.Integration.Tests.Flows;

// Individual tip: privatna grupa od jednog ucenika; profesor mora da potvrdi termin pre starta.
public class IndividualFlowTests : ApiTestBase
{
    public IndividualFlowTests(ApiFactory factory) : base(factory) { }

    private async Task<(Guid enrollmentId, Guid groupId)> Enroll(TestUser student, TestUser professor)
    {
        var response = await Post(student, "/enrollments/individual", new { courseId = CourseId, professorId = professor.Id });
        await AssertStatus(HttpStatusCode.Created, response);
        var json = await Json(response);
        return (json.GetProperty("enrollmentId").GetGuid(), json.GetProperty("groupId").GetGuid());
    }

    private Task<HttpResponseMessage> AssignSlot(TestUser staff, Guid groupId, TestUser professor, string time = "17:00:00") =>
        Post(staff, $"/groups/{groupId}/assign-professor-slot",
            new { slotIndex = 0, dayOfWeek = "Tuesday", time, professorId = professor.Id });

    [IntegrationFact]
    public async Task Individual_enrollment_creates_a_private_Ready_group_waiting_for_the_slot()
    {
        var student = TestUser.Student();
        var professor = TestUser.Professor();

        var (enrollmentId, groupId) = await Enroll(student, professor);

        Assert.Equal("Ready", await EnrollmentStatus(student, enrollmentId));
        var group = await GetGroup(professor, groupId);
        Assert.Equal("Individual", group.GetProperty("type").GetString());
        Assert.Equal("Ready", group.GetProperty("status").GetString());
        Assert.Equal(1, group.GetProperty("maxSize").GetInt32());
        Assert.Single(group.GetProperty("slots").EnumerateArray());
        Assert.False((await Access(student)).GetProperty("hasAccess").GetBoolean());

        // zahtjevi koji cekaju potvrdu termina
        var waiting = await Json(await Get(professor, "/groups?type=Individual&status=Ready"));
        Assert.Contains(waiting.EnumerateArray(), g => g.GetProperty("id").GetGuid() == groupId);
        // privatna grupa se ne nudi drugim ucenicima
        var available = await Json(await Get(TestUser.Student(), $"/groups/available?courseId={CourseId}"));
        Assert.Empty(available.EnumerateArray());
    }

    [IntegrationFact]
    public async Task Start_requires_the_slot_and_then_activates_access()
    {
        var student = TestUser.Student();
        var professor = TestUser.Professor();
        var (enrollmentId, groupId) = await Enroll(student, professor);

        var early = await Post(professor, $"/groups/{groupId}/start");
        await AssertStatus(HttpStatusCode.BadRequest, early);
        Assert.Equal("CANNOT_START", await ErrorCode(early));

        await AssertStatus(HttpStatusCode.NoContent, await AssignSlot(professor, groupId, professor));
        await AssertStatus(HttpStatusCode.NoContent, await Post(professor, $"/groups/{groupId}/start"));

        Assert.Equal("Active", await EnrollmentStatus(student, enrollmentId));
        Assert.True((await Access(student)).GetProperty("hasAccess").GetBoolean());
    }

    [IntegrationFact]
    public async Task The_same_slot_cannot_be_given_to_a_professor_who_is_already_busy_then()
    {
        var professor = TestUser.Professor();
        var (_, first) = await Enroll(TestUser.Student(), professor);
        await AssignSlot(professor, first, professor);

        var second = TestUser.Student();
        var other = TestUser.Student();
        Factory.Courses.Add(CourseId, "Spanish A1");
        // drugi kurs, isti profesor i isto vrijeme: konflikt
        var response = await Post(other, "/enrollments/individual", new { courseId = Guid.NewGuid(), professorId = professor.Id });
        await AssertStatus(HttpStatusCode.Created, response);
        var secondGroup = (await Json(response)).GetProperty("groupId").GetGuid();

        var conflict = await AssignSlot(professor, secondGroup, professor);
        await AssertStatus(HttpStatusCode.BadRequest, conflict);
        Assert.Equal("CANNOT_ASSIGN", await ErrorCode(conflict));
        await AssertStatus(HttpStatusCode.NoContent, await AssignSlot(professor, secondGroup, professor, "18:00:00"));
    }

    [IntegrationFact]
    public async Task Duplicate_enrollment_in_the_same_course_is_rejected()
    {
        var student = TestUser.Student();
        var professor = TestUser.Professor();
        await Enroll(student, professor);

        var again = await Post(student, "/enrollments/individual", new { courseId = CourseId, professorId = professor.Id });

        await AssertStatus(HttpStatusCode.BadRequest, again);
        Assert.Equal("INVALID_ENROLLMENT", await ErrorCode(again));
    }

    [IntegrationFact]
    public async Task Individual_enrollment_is_blocked_by_an_existing_group_enrollment()
    {
        var student = TestUser.Student();
        var professor = TestUser.Professor();
        await AddMember(professor, await CreateGroup(professor), student);

        var response = await Post(student, "/enrollments/individual", new { courseId = CourseId, professorId = professor.Id });

        await AssertStatus(HttpStatusCode.BadRequest, response);
    }

    [IntegrationFact]
    public async Task Requests_without_a_professor_are_rejected()
    {
        var student = TestUser.Student();

        var response = await Post(student, "/enrollments/individual", new { courseId = CourseId, professorId = Guid.Empty });

        await AssertStatus(HttpStatusCode.BadRequest, response);
    }

    [IntegrationFact]
    public async Task Cancelling_before_start_dissolves_the_private_group_and_allows_a_new_enrollment()
    {
        var student = TestUser.Student();
        var professor = TestUser.Professor();
        var (enrollmentId, groupId) = await Enroll(student, professor);
        await AssignSlot(professor, groupId, professor);

        await AssertStatus(HttpStatusCode.NoContent, await Post(student, $"/enrollments/{enrollmentId}/cancel"));

        Assert.Equal("Dissolved", (await GetGroup(professor, groupId)).GetProperty("status").GetString());
        Assert.Equal("Cancelled", await EnrollmentStatus(student, enrollmentId));

        // termin je slobodan i ucenik moze ponovo (otkazan upis ne blokira)
        var (newEnrollment, newGroup) = await Enroll(student, professor);
        await AssertStatus(HttpStatusCode.NoContent, await AssignSlot(professor, newGroup, professor));
        Assert.Equal("Ready", await EnrollmentStatus(student, newEnrollment));
    }

    [IntegrationFact]
    public async Task Cancelling_after_start_closes_the_empty_group()
    {
        var student = TestUser.Student();
        var professor = TestUser.Professor();
        var (enrollmentId, groupId) = await Enroll(student, professor);
        await AssignSlot(professor, groupId, professor);
        await Post(professor, $"/groups/{groupId}/start");

        await AssertStatus(HttpStatusCode.NoContent, await Post(student, $"/enrollments/{enrollmentId}/cancel"));

        Assert.Equal("Completed", (await GetGroup(professor, groupId)).GetProperty("status").GetString());
        Assert.False((await Access(student)).GetProperty("hasAccess").GetBoolean());
    }
}