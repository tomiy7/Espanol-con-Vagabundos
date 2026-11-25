using System.Diagnostics;
using System.Net;
using System.Text;
using GroupsService.Application.Interfaces;
using GroupsService.Infrastructure.Clients;
using GroupsService.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GroupsService.Integration.Tests.Resilience;

// Auth i Courses daju samo imena (best-effort): kad ne rade, vracaju gresku, smece ili
// ne odgovaraju, Groups mora da nastavi bez imena - nikad 500 i nikad dugo cekanje.
// Ovde se testiraju PRAVI klijenti (u ostalim testovima su zamenjeni lazima).
public class ExternalServiceClientTests
{
    private const string BaseUrl = "http://external.test";

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            respond(request, ct);
    }

    private static HttpResponseMessage Respond(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    // Ponasanja spoljnog servisa koja se proveravaju.
    public static TheoryData<string> Failures => new() { "500", "404", "garbage", "empty", "wrong-shape", "timeout", "refused" };

    private static HttpClient Client(string failure)
    {
        if (failure == "refused")
            return new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        var handler = new StubHandler(async (_, ct) => failure switch
        {
            "500" => Respond(HttpStatusCode.InternalServerError, """{"error":"boom"}"""),
            "404" => Respond(HttpStatusCode.NotFound, ""),
            "garbage" => Respond(HttpStatusCode.OK, "<html>not json</html>"),
            "empty" => Respond(HttpStatusCode.OK, ""),
            "wrong-shape" => Respond(HttpStatusCode.OK, """{"unexpected":true}"""),
            "timeout" => await Task.Delay(TimeSpan.FromSeconds(30), ct).ContinueWith(_ => Respond(HttpStatusCode.OK, "")),
            _ => throw new ArgumentOutOfRangeException(nameof(failure))
        });
        return new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(500) };
    }

    private static string Url(string failure) => failure == "refused" ? "http://127.0.0.1:1" : BaseUrl;

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task User_directory_returns_null_instead_of_failing(string failure)
    {
        var directory = new AuthServiceUserDirectory(Client(failure),
            Options.Create(new AuthServiceOptions { BaseUrl = Url(failure) }), NullLogger<AuthServiceUserDirectory>.Instance);

        var timer = Stopwatch.StartNew();
        var contact = await directory.GetUserContactAsync(Guid.NewGuid());

        Assert.Null(contact);
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(10), $"Took {timer.Elapsed.TotalSeconds:F1}s.");
    }

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task Course_directory_returns_null_instead_of_failing(string failure)
    {
        var directory = new CoursesServiceDirectory(Client(failure),
            Options.Create(new CoursesServiceOptions { BaseUrl = Url(failure) }), NullLogger<CoursesServiceDirectory>.Instance);

        var timer = Stopwatch.StartNew();
        var name = await directory.GetCourseNameAsync(Guid.NewGuid());

        Assert.Null(name);
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(10), $"Took {timer.Elapsed.TotalSeconds:F1}s.");
    }

    [Fact]
    public async Task User_lookup_calls_the_auth_admin_endpoint()
    {
        Uri? called = null;
        var directory = new AuthServiceUserDirectory(
            new HttpClient(new StubHandler((request, _) =>
            {
                called = request.RequestUri;
                return Task.FromResult(Respond(HttpStatusCode.NotFound, ""));
            })),
            Options.Create(new AuthServiceOptions { BaseUrl = BaseUrl + "/" }), NullLogger<AuthServiceUserDirectory>.Instance);
        var userId = Guid.NewGuid();

        await directory.GetUserContactAsync(userId);

        Assert.Equal($"{BaseUrl}/auth/admin/users/{userId}", called?.ToString());
    }

    [Fact]
    public async Task Healthy_services_return_the_data()
    {
        var userId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var users = new AuthServiceUserDirectory(
            new HttpClient(new StubHandler((_, _) => Task.FromResult(Respond(HttpStatusCode.OK,
                $$"""{"id":"{{userId}}","username":"ana","email":"ana@test.local","phone":"","firstName":"Ana","lastName":"Ivic","role":"student","createdAt":"2026-01-01T00:00:00Z"}""")))),
            Options.Create(new AuthServiceOptions { BaseUrl = BaseUrl }), NullLogger<AuthServiceUserDirectory>.Instance);
        var courses = new CoursesServiceDirectory(
            new HttpClient(new StubHandler((_, _) => Task.FromResult(Respond(HttpStatusCode.OK,
                $$"""[{"id":"{{courseId}}","name":"Spanish A1","slug":"a1","price":10}]""")))),
            Options.Create(new CoursesServiceOptions { BaseUrl = BaseUrl }), NullLogger<CoursesServiceDirectory>.Instance);

        Assert.Equal("Ana Ivic", (await users.GetUserContactAsync(userId))?.FullName);
        Assert.Equal("Spanish A1", await courses.GetCourseNameAsync(courseId));
    }
}

// Isto, ali kroz ceo API: endpoint-i koji prikazuju imena rade i kad Auth/Courses ne rade.
public class ExternalServicesOutageTests : ApiTestBase
{
    public ExternalServicesOutageTests(ApiFactory factory) : base(factory) { }

    [IntegrationFact]
    public async Task Endpoints_still_answer_without_names_when_auth_and_courses_are_down()
    {
        var professor = TestUser.Professor();
        var student = TestUser.Student();
        var groupId = await CreateGroup(professor);
        await AddMember(professor, groupId, student);

        await using var api = Factory.Variant(services =>
        {
            services.RemoveAll<IUserDirectory>();
            services.RemoveAll<ICourseDirectory>();
            services.AddHttpClient<IUserDirectory, AuthServiceUserDirectory>(c => c.Timeout = TimeSpan.FromSeconds(5));
            services.AddHttpClient<ICourseDirectory, CoursesServiceDirectory>(c => c.Timeout = TimeSpan.FromSeconds(5));
            services.Configure<AuthServiceOptions>(o => o.BaseUrl = "http://127.0.0.1:1");
            services.Configure<CoursesServiceOptions>(o => o.BaseUrl = "http://127.0.0.1:1");
        });

        var group = await ClientFor(api, professor).GetAsync($"/groups/{groupId}");
        await AssertStatus(HttpStatusCode.OK, group);

        var mine = await ClientFor(api, student).GetAsync("/enrollments/mine");
        await AssertStatus(HttpStatusCode.OK, mine);
        var enrollment = (await Json(mine)).EnumerateArray().Single();
        Assert.Equal(System.Text.Json.JsonValueKind.Null, enrollment.GetProperty("courseName").ValueKind);
    }
}
