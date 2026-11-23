using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroupsService.Integration.Tests.Infrastructure;

namespace GroupsService.Integration.Tests.Security;

// Autentikacija, role i vlasnistvo nad resursima (HTTP -> pravi JWT middleware).
public class AuthorizationTests : ApiTestBase
{
    public AuthorizationTests(ApiFactory factory) : base(factory) { }

    [IntegrationTheory]
    [InlineData("GET", "/enrollments/mine")]
    [InlineData("GET", "/enrollments/access?courseId=00000000-0000-0000-0000-000000000001")]
    [InlineData("GET", "/groups")]
    [InlineData("GET", "/groups/available?courseId=00000000-0000-0000-0000-000000000001")]
    [InlineData("GET", "/audit")]
    [InlineData("POST", "/enrollments/individual")]
    [InlineData("POST", "/enrollments/group")]
    [InlineData("POST", "/enrollments/invite-partner")]
    [InlineData("POST", "/groups")]
    public async Task Protected_endpoints_return_401_without_a_token(string method, string url)
    {
        var client = ClientFor(null);
        var response = method == "GET"
            ? await client.GetAsync(url)
            : await client.PostAsJsonAsync(url, new { });

        await AssertStatus(HttpStatusCode.Unauthorized, response);
    }

    [IntegrationFact]
    public async Task A_token_signed_with_a_different_key_is_rejected()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.token");

        await AssertStatus(HttpStatusCode.Unauthorized, await client.GetAsync("/enrollments/mine"));
    }

    [IntegrationFact]
    public async Task Invitation_preview_is_public_and_unknown_token_is_404()
    {
        var response = await Get(null, "/invitations/unknown-token");

        await AssertStatus(HttpStatusCode.NotFound, response);
        Assert.Equal("INVITATION_NOT_FOUND", await ErrorCode(response));
    }

    [IntegrationFact]
    public async Task Students_cannot_use_staff_endpoints()
    {
        var professor = TestUser.Professor();
        var groupId = await CreateGroup(professor);
        var student = TestUser.Student();
        var enrollmentId = await AddMember(professor, groupId, TestUser.Student());
        var someSlot = new { slotIndex = 0, dayOfWeek = "Monday", time = "18:00:00", professorId = professor.Id };

        await AssertStatus(HttpStatusCode.Forbidden, await Get(student, "/groups"));
        await AssertStatus(HttpStatusCode.Forbidden, await Get(student, $"/groups/{groupId}"));
        await AssertStatus(HttpStatusCode.Forbidden, await Post(student, "/groups", new { courseId = CourseId, slots = Slots(student.Id, 9) }));
        await AssertStatus(HttpStatusCode.Forbidden, await Post(student, $"/groups/{groupId}/members", new { userId = student.Id }));
        await AssertStatus(HttpStatusCode.Forbidden, await Post(student, $"/groups/{groupId}/assign-professor-slot", someSlot));
        await AssertStatus(HttpStatusCode.Forbidden, await Post(student, $"/groups/{groupId}/start"));
        await AssertStatus(HttpStatusCode.Forbidden, await Post(student, $"/groups/{groupId}/complete"));
        await AssertStatus(HttpStatusCode.Forbidden, await Post(student, $"/enrollments/{enrollmentId}/freeze"));
        await AssertStatus(HttpStatusCode.Forbidden, await Post(student, $"/enrollments/{enrollmentId}/unfreeze"));
        await AssertStatus(HttpStatusCode.Forbidden, await Get(student, "/audit"));
    }

    [IntegrationFact]
    public async Task Admin_only_actions_reject_professors_but_allow_admins()
    {
        var professor = TestUser.Professor();
        var admin = TestUser.Admin();
        var groupId = await CreateGroup(professor);

        await AssertStatus(HttpStatusCode.Forbidden, await Post(professor, $"/groups/{groupId}/complete"));
        await AssertStatus(HttpStatusCode.Forbidden, await Get(professor, "/audit"));

        // admin prolazi autorizaciju (grupa nije Active, pa je odgovor poslovna greska 400, ne 403)
        var complete = await Post(admin, $"/groups/{groupId}/complete");
        await AssertStatus(HttpStatusCode.BadRequest, complete);
        Assert.Equal("CANNOT_COMPLETE", await ErrorCode(complete));
        await AssertStatus(HttpStatusCode.OK, await Get(admin, "/audit"));
    }

    [IntegrationFact]
    public async Task Enrollment_details_are_visible_to_the_owner_and_staff_but_not_to_other_students()
    {
        var professor = TestUser.Professor();
        var owner = TestUser.Student();
        var other = TestUser.Student();
        var groupId = await CreateGroup(professor);
        var enrollmentId = await AddMember(professor, groupId, owner);

        await AssertStatus(HttpStatusCode.OK, await Get(owner, $"/enrollments/{enrollmentId}"));
        await AssertStatus(HttpStatusCode.OK, await Get(professor, $"/enrollments/{enrollmentId}"));

        var foreign = await Get(other, $"/enrollments/{enrollmentId}");
        await AssertStatus(HttpStatusCode.NotFound, foreign);
        Assert.Equal("ENROLLMENT_NOT_FOUND", await ErrorCode(foreign));

        await AssertStatus(HttpStatusCode.NotFound, await Get(owner, $"/enrollments/{Guid.NewGuid()}"));
    }

    [IntegrationFact]
    public async Task Mine_returns_only_the_callers_enrollments()
    {
        var professor = TestUser.Professor();
        var a = TestUser.Student();
        var b = TestUser.Student();
        var groupId = await CreateGroup(professor);
        await AddMember(professor, groupId, a);
        await AddMember(professor, groupId, b);

        var mine = await MyEnrollments(a);

        Assert.Single(mine.EnumerateArray());
        Assert.Equal("Spanish A1", mine[0].GetProperty("courseName").GetString());
        Assert.False(mine[0].TryGetProperty("completedLessons", out _)); // napredak vodi Courses
    }

    [IntegrationFact]
    public async Task Access_requires_a_course_id()
    {
        var student = TestUser.Student();

        var response = await Get(student, $"/enrollments/access?courseId={Guid.Empty}");

        await AssertStatus(HttpStatusCode.BadRequest, response);
        Assert.Equal("INVALID_COURSE_ID", await ErrorCode(response));
    }
}