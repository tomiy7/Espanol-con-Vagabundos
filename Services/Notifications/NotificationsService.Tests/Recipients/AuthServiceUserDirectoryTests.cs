using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using NotificationsService.API.Options;
using NotificationsService.API.Recipients;

namespace NotificationsService.Tests.Recipients;

// Pravi Auth klijent: tacna ruta, admin token, i razlika "korisnik ne postoji" (null,
// mejl se preskace) od "Auth ne radi" (izuzetak, poruka se ponavlja).
public class AuthServiceUserDirectoryTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? Last { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Last = request;
            return Task.FromResult(respond(request));
        }
    }

    private static (AuthServiceUserDirectory Directory, StubHandler Handler) Create(HttpStatusCode status, string body = "")
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });
        var directory = new AuthServiceUserDirectory(new HttpClient(handler),
            Microsoft.Extensions.Options.Options.Create(new AuthServiceOptions { BaseUrl = "http://auth.test/", ServiceAdminToken = "admin-token" }));
        return (directory, handler);
    }

    [Fact]
    public async Task Reads_the_user_from_the_auth_admin_endpoint_with_the_service_token()
    {
        var userId = Guid.NewGuid();
        var (directory, handler) = Create(HttpStatusCode.OK,
            $$"""{"id":"{{userId}}","username":"ana","email":"ana@test.local","firstName":"Ana","lastName":"Ivić","role":"student"}""");

        var contact = await directory.GetUserContactAsync(userId, CancellationToken.None);

        Assert.Equal(new UserContact(userId, "ana@test.local", "Ana Ivić"), contact);
        Assert.Equal($"http://auth.test/auth/admin/users/{userId}", handler.Last!.RequestUri!.ToString());
        Assert.Equal("Bearer admin-token", handler.Last.Headers.Authorization!.ToString());
    }

    [Fact]
    public async Task A_user_that_does_not_exist_is_null()
    {
        var (directory, _) = Create(HttpStatusCode.NotFound, """{"error":"USER_NOT_FOUND"}""");
        Assert.Null(await directory.GetUserContactAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task A_user_without_an_email_is_null()
    {
        var (directory, _) = Create(HttpStatusCode.OK, $$"""{"id":"{{Guid.NewGuid()}}","email":""}""");
        Assert.Null(await directory.GetUserContactAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Auth_problems_are_errors_so_the_message_is_retried(HttpStatusCode status)
    {
        var (directory, _) = Create(status);
        await Assert.ThrowsAsync<HttpRequestException>(() => directory.GetUserContactAsync(Guid.NewGuid(), CancellationToken.None));
    }
}
