using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GroupsService.Smoke.Tests;

// Kratka provera da pokrenut servis (image, konfiguracija, migracija, baza, JWT) radi.
// Poslovna pravila pokrivaju integracioni testovi; ovde je samo nekoliko kljucnih poziva.
// Svaki test pravi svoje podatke (nov courseId), pa moze vise puta nad istom bazom.
public class GroupsSmokeTests
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private static Task<HttpResponseMessage> Send(HttpMethod method, string path, SmokeUser? user = null, object? body = null)
    {
        var request = new HttpRequestMessage(method, SmokeSettings.BaseUrl + path);
        if (user is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return Http.SendAsync(request);
    }

    private static Task<HttpResponseMessage> Get(string path, SmokeUser? user = null) => Send(HttpMethod.Get, path, user);

    private static Task<HttpResponseMessage> Post(string path, SmokeUser? user = null, object? body = null) =>
        Send(HttpMethod.Post, path, user, body ?? new { });

    private static async Task Expect(HttpStatusCode expected, HttpResponseMessage response)
    {
        if (response.StatusCode != expected)
            Assert.Fail($"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri}: expected " +
                        $"{(int)expected} but got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    [SmokeFact]
    public async Task Service_is_healthy_and_reaches_its_database()
    {
        var response = await Get("/health");

        await Expect(HttpStatusCode.OK, response);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [SmokeFact]
    public async Task Protected_routes_require_a_token()
    {
        await Expect(HttpStatusCode.Unauthorized, await Get("/enrollments/mine"));
        await Expect(HttpStatusCode.Unauthorized, await Get("/groups"));
    }

    [SmokeFact]
    public async Task Errors_are_problem_details_with_a_code()
    {
        var response = await Get($"/invitations/smoke-{Guid.NewGuid():N}");

        await Expect(HttpStatusCode.NotFound, response);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("INVITATION_NOT_FOUND", (await Json(response)).GetProperty("code").GetString());
    }

    [SmokeFlowFact]
    public async Task Token_is_accepted_and_roles_are_enforced()
    {
        var student = await SmokeUsers.Student();

        await Expect(HttpStatusCode.OK, await Get("/enrollments/mine", student));
        await Expect(HttpStatusCode.Forbidden, await Get("/groups", student));
    }

    // Glavni tok: grupa -> upis -> start -> pristup -> zatvaranje -> audit. Grupa se na
    // kraju zatvara, pa termin profesora ostaje slobodan za sledece pokretanje.
    [SmokeFlowFact]
    public async Task Group_lifecycle_works_end_to_end()
    {
        var admin = await SmokeUsers.Admin();
        var professor = await SmokeUsers.Professor();
        var students = new[] { await SmokeUsers.Student(), await SmokeUsers.Student(), await SmokeUsers.Student() };
        var courseId = Guid.NewGuid();

        // nasumicno vreme, da se pokretanje sa istim (pravim) profesorom ne sudari sa ranijim
        var time = $"{Random.Shared.Next(6, 23):00}:{Random.Shared.Next(0, 60):00}:00";
        var slots = new[] { "Monday", "Wednesday", "Friday" }
            .Select(day => new { dayOfWeek = day, time, professorId = professor.Id }).ToArray();

        var created = await Post("/groups", professor, new { courseId, slots });
        await Expect(HttpStatusCode.Created, created);
        Assert.NotNull(created.Headers.Location);
        var groupId = (await Json(created)).GetProperty("groupId").GetGuid();

        var available = await Get($"/groups/available?courseId={courseId}", students[0]);
        await Expect(HttpStatusCode.OK, available);
        Assert.Contains((await Json(available)).EnumerateArray(), g => g.GetProperty("id").GetGuid() == groupId);

        // prvi ucenik se upisuje sam, ostale dodaje osoblje
        await Expect(HttpStatusCode.Created, await Post("/enrollments/group", students[0], new { groupId }));
        foreach (var student in students.Skip(1))
            await Expect(HttpStatusCode.Created, await Post($"/groups/{groupId}/members", professor, new { userId = student.Id }));

        await Expect(HttpStatusCode.NoContent, await Post($"/groups/{groupId}/start", professor));

        var access = await Get($"/enrollments/access?courseId={courseId}", students[0]);
        await Expect(HttpStatusCode.OK, access);
        Assert.True((await Json(access)).GetProperty("hasAccess").GetBoolean());

        await Expect(HttpStatusCode.NoContent, await Post($"/groups/{groupId}/complete", admin));

        var audit = await Get($"/audit?entityId={groupId}", admin);
        await Expect(HttpStatusCode.OK, audit);
        Assert.True((await Json(audit)).GetArrayLength() >= 4);
    }
}
