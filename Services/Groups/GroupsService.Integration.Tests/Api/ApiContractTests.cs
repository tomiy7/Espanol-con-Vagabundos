using System.Net;
using System.Text;
using GroupsService.Integration.Tests.Infrastructure;

namespace GroupsService.Integration.Tests.Api;

// Ugovor koji front koristi bez obzira na funkcionalnost: oblik gresaka (ProblemDetails
// sa "code"), statusi po vrsti greske i Location zaglavlje za nove resurse.
public class ApiContractTests : ApiTestBase
{
    public ApiContractTests(ApiFactory factory) : base(factory) { }

    [IntegrationFact]
    public async Task Health_endpoint_is_anonymous_and_checks_the_database()
    {
        var response = await Get(null, "/health");

        await AssertStatus(HttpStatusCode.OK, response);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [IntegrationFact]
    public async Task Business_errors_are_problem_details_with_a_stable_code()
    {
        var professor = TestUser.Professor();
        var groupId = await CreateGroup(professor);
        await AddMember(professor, groupId, TestUser.Student()); // 1 od minimalno 3

        var response = await Post(professor, $"/groups/{groupId}/start");

        await AssertProblem(HttpStatusCode.BadRequest, "CANNOT_START", response);
        var problem = await Json(response);
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("title").GetString()));
        Assert.Contains("at least", problem.GetProperty("detail").GetString());
        Assert.True(problem.TryGetProperty("traceId", out _));
    }

    [IntegrationFact]
    public async Task Missing_resources_inside_commands_return_404_with_the_entity_code()
    {
        var professor = TestUser.Professor();

        await AssertProblem(HttpStatusCode.NotFound, "GROUP_NOT_FOUND", await Post(professor, $"/groups/{Guid.NewGuid()}/start"));
        await AssertProblem(HttpStatusCode.NotFound, "ENROLLMENT_NOT_FOUND",
            await Post(professor, $"/enrollments/{Guid.NewGuid()}/unfreeze"));
        await AssertProblem(HttpStatusCode.NotFound, "GROUP_NOT_FOUND",
            await Post(TestUser.Student(), "/enrollments/group", new { groupId = Guid.NewGuid() }));
    }

    [IntegrationFact]
    public async Task Malformed_json_returns_a_validation_problem_with_a_code()
    {
        var client = ClientFor(TestUser.Professor());

        var response = await client.PostAsync("/groups",
            new StringContent("{ this is not json", Encoding.UTF8, "application/json"));

        await AssertProblem(HttpStatusCode.BadRequest, "VALIDATION_FAILED", response);
    }

    [IntegrationFact]
    public async Task Created_resources_return_a_location_that_can_be_read()
    {
        var professor = TestUser.Professor();
        var student = TestUser.Student();

        var created = await Post(professor, "/groups", new { courseId = CourseId, slots = Slots(professor.Id) });
        await AssertStatus(HttpStatusCode.Created, created);
        Assert.NotNull(created.Headers.Location);
        await AssertStatus(HttpStatusCode.OK, await ClientFor(professor).GetAsync(created.Headers.Location));

        var groupId = (await Json(created)).GetProperty("groupId").GetGuid();
        var joined = await Post(student, "/enrollments/group", new { groupId });
        await AssertStatus(HttpStatusCode.Created, joined);
        var enrollment = await Json(await ClientFor(student).GetAsync(joined.Headers.Location));
        Assert.Equal(student.Id, enrollment.GetProperty("userId").GetGuid());
    }

    [IntegrationFact]
    public async Task Commands_without_a_result_return_204_with_an_empty_body()
    {
        var (groupId, professor, _, _) = await ReadyGroup();

        var response = await Post(professor, $"/groups/{groupId}/start");

        await AssertStatus(HttpStatusCode.NoContent, response);
        Assert.Empty(await response.Content.ReadAsStringAsync());
    }

    [IntegrationFact]
    public async Task Authorization_failures_have_no_body()
    {
        var forbidden = await Get(TestUser.Student(), "/audit");
        var unauthorized = await Get(null, "/audit");

        await AssertStatus(HttpStatusCode.Forbidden, forbidden);
        await AssertStatus(HttpStatusCode.Unauthorized, unauthorized);
        Assert.Empty(await forbidden.Content.ReadAsStringAsync());
    }
}
