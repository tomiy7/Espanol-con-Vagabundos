using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GroupsService.Smoke.Tests;

public sealed record SmokeUser(Guid Id, string Token);

// Pravi korisnike za smoke testove: ili potpisuje token tajnim kljucem (lokalno/CI),
// ili ide kroz pravi Auth (login osoblja, registracija ucenika).
public static class SmokeUsers
{
    // Kratki nazivi claim-ova, isti kao u tokenu koji izdaje Auth (JwtSecurityTokenHandler
    // mapira ClaimTypes.NameIdentifier -> "nameid" i ClaimTypes.Role -> "role").
    private const string NameIdentifier = "nameid";
    private const string Role = "role";

    public static Task<SmokeUser> Admin() =>
        SmokeSettings.JwtSecret is not null
            ? Task.FromResult(Signed("admin"))
            : Login(SmokeSettings.AdminLogin!, SmokeSettings.AdminPassword!);

    public static Task<SmokeUser> Professor() =>
        SmokeSettings.JwtSecret is not null
            ? Task.FromResult(Signed("professor"))
            : Login(SmokeSettings.ProfessorLogin!, SmokeSettings.ProfessorPassword!);

    public static Task<SmokeUser> Student() =>
        SmokeSettings.JwtSecret is not null
            ? Task.FromResult(Signed("student"))
            : RegisterStudent();

    // HS256 token u istom obliku kao Auth-ov (nameid + role).
    private static SmokeUser Signed(string role)
    {
        var id = Guid.NewGuid();
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "HS256", typ = "JWT" }));
        var payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            [NameIdentifier] = id.ToString(),
            [Role] = role,
            ["exp"] = DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds()
        }));
        var key = Encoding.UTF8.GetBytes(SmokeSettings.JwtSecret!);
        var signature = Base64Url(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"{header}.{payload}")));
        return new SmokeUser(id, $"{header}.{payload}.{signature}");
    }

    private static async Task<SmokeUser> Login(string identifier, string password)
    {
        using var client = new HttpClient();
        var response = await client.PostAsJsonAsync($"{SmokeSettings.AuthUrl}/auth/login", new { identifier, password });
        response.EnsureSuccessStatusCode();
        return FromAuthResponse(await response.Content.ReadFromJsonAsync<JsonElement>());
    }

    // Ucenik se registruje kao pravi korisnik (Auth dodeljuje student rolu).
    private static async Task<SmokeUser> RegisterStudent()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var password = $"Smoke-{Guid.NewGuid():N}";
        using var client = new HttpClient();
        var response = await client.PostAsJsonAsync($"{SmokeSettings.AuthUrl}/auth/register", new
        {
            username = $"smoke_{suffix}",
            email = $"smoke_{suffix}@example.com",
            phone = "0601234567",
            password,
            confirmPassword = password,
            firstName = "Smoke",
            lastName = "Test"
        });
        response.EnsureSuccessStatusCode();
        return FromAuthResponse(await response.Content.ReadFromJsonAsync<JsonElement>());
    }

    private static SmokeUser FromAuthResponse(JsonElement body)
    {
        var token = body.GetProperty("accessToken").GetString()!;
        var payload = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        using var claims = JsonDocument.Parse(Convert.FromBase64String(payload));
        var id = Guid.Parse(claims.RootElement.GetProperty(NameIdentifier).GetString()!);
        return new SmokeUser(id, token);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
