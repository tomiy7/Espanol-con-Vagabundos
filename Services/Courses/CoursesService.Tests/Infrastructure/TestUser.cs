using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace CoursesService.Tests.Infrastructure;

// Korisnik u testu: Id i rola idu u JWT (isti claim-ovi koje izdaje Auth).
public record TestUser(Guid Id, string Role)
{
    public static TestUser Student() => new(Guid.NewGuid(), "student");
    public static TestUser Professor() => new(Guid.NewGuid(), "professor");
    public static TestUser Admin() => new(Guid.NewGuid(), "admin");

    public string Token(string secret = CoursesFactory.JwtSecret)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var token = new JwtSecurityToken(
            claims: [new Claim(ClaimTypes.NameIdentifier, Id.ToString()), new Claim(ClaimTypes.Role, Role)],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public HttpClient Client(CoursesFactory factory, string? token = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token ?? Token());
        return client;
    }
}
