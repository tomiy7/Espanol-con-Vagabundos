using System.Net;
using System.Text;
using CoursesService.API.Access;
using CoursesService.API.Options;
using Microsoft.Extensions.Options;

namespace CoursesService.Tests.Unit;

// Klijent prema Groups-u: tacan URL i token, citanje odgovora i prevodjenje kvarova.
public class GroupsAccessClientTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? Last { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Last = request;
            return Task.FromResult(respond(request));
        }
    }

    private static (GroupsAccessClient Client, StubHandler Handler) Create(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new StubHandler(respond);
        var options = Options.Create(new GroupsServiceOptions { BaseUrl = "http://groups.test/" });
        return (new GroupsAccessClient(new HttpClient(handler), options), handler);
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Reads_the_groups_response_and_sends_the_user_token()
    {
        var courseId = Guid.NewGuid();
        var enrollmentId = Guid.NewGuid();
        var (client, handler) = Create(_ =>
            Json($$"""{"hasAccess":true,"enrollmentId":"{{enrollmentId}}","unlockedLessons":6}"""));

        var access = await client.CheckAsync("user-jwt", courseId, CancellationToken.None);

        Assert.Equal(new CourseAccess(true, enrollmentId, 6), access);
        Assert.Equal($"http://groups.test/enrollments/access?courseId={courseId}", handler.Last!.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.Last.Headers.Authorization!.Scheme);
        Assert.Equal("user-jwt", handler.Last.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task Rejected_token_is_reported_as_such()
    {
        var (client, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        await Assert.ThrowsAsync<GroupsRejectedTokenException>(() => client.CheckAsync("t", Guid.NewGuid(), CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Other_failures_mean_groups_is_unavailable(HttpStatusCode status)
    {
        var (client, _) = Create(_ => new HttpResponseMessage(status));

        await Assert.ThrowsAsync<GroupsUnavailableException>(() => client.CheckAsync("t", Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Unreadable_body_means_groups_is_unavailable()
    {
        var (client, _) = Create(_ => Json("<html>proxy error</html>"));

        await Assert.ThrowsAsync<GroupsUnavailableException>(() => client.CheckAsync("t", Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Connection_failure_means_groups_is_unavailable()
    {
        var (client, _) = Create(_ => throw new HttpRequestException("connection refused"));

        await Assert.ThrowsAsync<GroupsUnavailableException>(() => client.CheckAsync("t", Guid.NewGuid(), CancellationToken.None));
    }
}
