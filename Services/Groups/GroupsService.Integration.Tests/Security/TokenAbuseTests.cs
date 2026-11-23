using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using GroupsService.Integration.Tests.Infrastructure;
using Microsoft.IdentityModel.Tokens;

namespace GroupsService.Integration.Tests.Security;

// Tokeni koje bi neko mogao da podmetne: istekao, bez korisnika, sa izmisljenom ulogom,
// nepotpisan, izmenjen. Ispravan potpis sam po sebi nije dovoljan.
public class TokenAbuseTests : ApiTestBase
{
    public TokenAbuseTests(ApiFactory factory) : base(factory) { }

    private static string Sign(IEnumerable<Claim> claims, DateTime? expires = null, DateTime? notBefore = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ApiFactory.JwtSecret));
        var token = new JwtSecurityToken(
            claims: claims,
            notBefore: notBefore,
            expires: expires ?? DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private Task<HttpResponseMessage> GetWith(string token, string url)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.GetAsync(url);
    }

    private static Claim Id(Guid id) => new(ClaimTypes.NameIdentifier, id.ToString());
    private static Claim Role(string role) => new(ClaimTypes.Role, role);

    [IntegrationFact]
    public async Task An_expired_token_is_rejected()
    {
        var token = Sign(new[] { Id(Guid.NewGuid()), Role("admin") },
            expires: DateTime.UtcNow.AddMinutes(-10), notBefore: DateTime.UtcNow.AddHours(-1));
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetWith(token, "/groups")).StatusCode);
    }

    [IntegrationFact]
    public async Task A_token_that_is_not_valid_yet_is_rejected()
    {
        var token = Sign(new[] { Id(Guid.NewGuid()), Role("admin") },
            expires: DateTime.UtcNow.AddHours(2), notBefore: DateTime.UtcNow.AddHours(1));
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetWith(token, "/groups")).StatusCode);
    }

    [IntegrationFact]
    public async Task An_unsigned_token_is_rejected()
    {
        static string Part(string json) => Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(json));
        var token = $"{Part("""{"alg":"none","typ":"JWT"}""")}.{Part($$"""{"nameid":"{{Guid.NewGuid()}}","role":"admin","exp":4102444800}""")}.";
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetWith(token, "/groups")).StatusCode);
    }

    [IntegrationFact]
    public async Task Changing_the_role_inside_a_signed_token_breaks_the_signature()
    {
        var student = Sign(new[] { Id(Guid.NewGuid()), Role("student") });
        var parts = student.Split('.');
        var payload = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(parts[1])).Replace("student", "admin");
        var forged = $"{parts[0]}.{Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(payload))}.{parts[2]}";

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetWith(forged, "/groups")).StatusCode);
    }

    [IntegrationTheory]
    [InlineData("superadmin")]
    [InlineData("Admin ")]
    [InlineData("")]
    public async Task An_unknown_role_gets_no_staff_rights(string role)
    {
        var token = Sign(new[] { Id(Guid.NewGuid()), Role(role) });
        Assert.Equal(HttpStatusCode.Forbidden, (await GetWith(token, "/groups")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await GetWith(token, "/audit")).StatusCode);
    }

    [IntegrationTheory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task A_token_without_a_valid_user_cannot_act_as_anyone(string? userId)
    {
        var claims = new List<Claim> { Role("student") };
        if (userId is not null) claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
        var token = Sign(claims);

        var mine = await GetWith(token, "/enrollments/mine");
        await AssertProblem(HttpStatusCode.Unauthorized, "UNAUTHORIZED", mine);

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var enroll = await client.PostAsync("/enrollments/individual", System.Net.Http.Json.JsonContent.Create(
            new { courseId = CourseId, professorId = Guid.NewGuid() }));
        await AssertProblem(HttpStatusCode.Unauthorized, "UNAUTHORIZED", enroll);
        Assert.Equal(0, Factory.WithDb(db => db.Enrollments.Count()));
    }

    [IntegrationFact]
    public async Task A_garbage_authorization_header_is_rejected()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetWith("not.a.jwt", "/enrollments/mine")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetWith(new string('x', 10_000), "/enrollments/mine")).StatusCode);
    }
}
