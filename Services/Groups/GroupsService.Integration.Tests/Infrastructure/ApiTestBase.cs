using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;

namespace GroupsService.Integration.Tests.Infrastructure;

// Osnova za HTTP testove: prazna baza pre svakog testa, JWT za izabranog korisnika i
// pomocni scenariji (grupa, clanovi, upisi). Asertacije gresaka citaju ProblemDetails.
[Collection(DatabaseCollection.Name)]
public abstract class ApiTestBase : IAsyncLifetime
{
    protected readonly ApiFactory Factory;
    protected readonly Guid CourseId = Guid.NewGuid();

    protected ApiTestBase(ApiFactory factory)
    {
        Factory = factory;
    }

    public async Task InitializeAsync()
    {
        await Factory.ResetAsync();
        Factory.Courses.Add(CourseId, "Spanish A1");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ---------- HTTP pomocne ----------

    private static string Token(TestUser user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ApiFactory.JwtSecret));
        var token = new JwtSecurityToken(
            claims: new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Role, user.Role)
            },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    protected HttpClient ClientFor(TestUser? user) => ClientFor(Factory, user);

    // Klijent za varijantu API-ja (vidi ApiFactory.Variant).
    protected static HttpClient ClientFor(WebApplicationFactory<Program> factory, TestUser? user)
    {
        var client = factory.CreateClient();
        if (user is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(user));
        return client;
    }

    protected Task<HttpResponseMessage> Get(TestUser? user, string url) => ClientFor(user).GetAsync(url);

    protected Task<HttpResponseMessage> Post(TestUser? user, string url, object? body = null) =>
        ClientFor(user).PostAsJsonAsync(url, body ?? new { });

    protected static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "null" : text);
        return doc.RootElement.Clone();
    }

    protected static async Task AssertStatus(HttpStatusCode expected, HttpResponseMessage response)
    {
        if (response.StatusCode != expected)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.Fail($"Expected {(int)expected} {expected} but got {(int)response.StatusCode} {response.StatusCode}: {body}");
        }
    }

    // Greske su ProblemDetails (application/problem+json) sa stabilnim "code" poljem.
    protected static async Task<string> ErrorCode(HttpResponseMessage response) =>
        (await Json(response)).GetProperty("code").GetString()!;

    protected static async Task<string> ErrorDetail(HttpResponseMessage response) =>
        (await Json(response)).GetProperty("detail").GetString()!;

    protected static async Task AssertProblem(HttpStatusCode expected, string code, HttpResponseMessage response)
    {
        await AssertStatus(expected, response);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(code, await ErrorCode(response));
    }

    // Za asinhrone posledice (poruke, pozadinski servisi): ceka dok uslov ne postane tacan.
    protected static async Task Eventually(Func<bool> condition, string because, int seconds = 15)
    {
        var until = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < until)
        {
            if (condition()) return;
            await Task.Delay(100);
        }
        Assert.Fail($"Timed out waiting until {because}.");
    }

    // ---------- scenario pomocne ----------

    // Tri termina pon/sre/pet u 18:00 sa istim profesorom (svaki test koristi svoje vreme da nema konflikta).
    protected static object[] Slots(Guid professorId, int hour = 18) => new object[]
    {
        new { dayOfWeek = "Monday", time = $"{hour}:00:00", professorId },
        new { dayOfWeek = "Wednesday", time = $"{hour}:00:00", professorId },
        new { dayOfWeek = "Friday", time = $"{hour}:00:00", professorId }
    };

    protected async Task<Guid> CreateGroup(TestUser staff, Guid? professorId = null, int hour = 18)
    {
        var response = await Post(staff, "/groups", new
        {
            courseId = CourseId,
            slots = Slots(professorId ?? staff.Id, hour)
        });
        await AssertStatus(HttpStatusCode.Created, response);
        return (await Json(response)).GetProperty("groupId").GetGuid();
    }

    protected async Task<Guid> AddMember(TestUser staff, Guid groupId, TestUser student)
    {
        var response = await Post(staff, $"/groups/{groupId}/members", new { userId = student.Id });
        await AssertStatus(HttpStatusCode.Created, response);
        return (await Json(response)).GetProperty("enrollmentId").GetGuid();
    }

    protected async Task<JsonElement> GetGroup(TestUser staff, Guid groupId)
    {
        var response = await Get(staff, $"/groups/{groupId}");
        await AssertStatus(HttpStatusCode.OK, response);
        return await Json(response);
    }

    protected async Task<JsonElement> MyEnrollments(TestUser student)
    {
        var response = await Get(student, "/enrollments/mine");
        await AssertStatus(HttpStatusCode.OK, response);
        return await Json(response);
    }

    protected async Task<string> EnrollmentStatus(TestUser student, Guid enrollmentId)
    {
        var all = await MyEnrollments(student);
        return all.EnumerateArray().First(e => e.GetProperty("id").GetGuid() == enrollmentId)
            .GetProperty("status").GetString()!;
    }

    protected async Task<JsonElement> Access(TestUser student)
    {
        var response = await Get(student, $"/enrollments/access?courseId={CourseId}");
        await AssertStatus(HttpStatusCode.OK, response);
        return await Json(response);
    }

    // Grupa sa 3 clana spremna za start (Ready).
    protected async Task<(Guid groupId, TestUser professor, List<TestUser> students, List<Guid> enrollmentIds)> ReadyGroup(
        int hour = 18)
    {
        var professor = TestUser.Professor();
        var groupId = await CreateGroup(professor, hour: hour);
        var students = new List<TestUser>();
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var student = TestUser.Student();
            students.Add(student);
            ids.Add(await AddMember(professor, groupId, student));
        }
        return (groupId, professor, students, ids);
    }
}