using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using NotificationsService.API.Options;

namespace NotificationsService.API.Recipients;

public sealed record UserContact(Guid UserId, string Email, string FullName);

// Mejl i ime korisnika. Razlika u odnosu na Groups: ovde je bitno da li korisnik NE
// POSTOJI (null - mejl se preskace) ili je Auth NEDOSTUPAN (izuzetak - poruka se ponavlja,
// inace bi se mejl tiho izgubio).
public interface IUserDirectory
{
    Task<UserContact?> GetUserContactAsync(Guid userId, CancellationToken cancellationToken);
}

// Naziv kursa je samo ukras u mejlu - nedostupan Courses ne sme da zaustavi slanje.
public interface ICourseDirectory
{
    Task<string?> GetCourseNameAsync(Guid courseId, CancellationToken cancellationToken);
}

public sealed class AuthServiceUserDirectory : IUserDirectory
{
    // Oblik odgovora iz AuthService.API.DTOs.UserDetailResponseDto (samo polja koja trebaju).
    private sealed record AuthUserDto(Guid Id, string? Email, string? FirstName, string? LastName);

    private readonly HttpClient _http;
    private readonly AuthServiceOptions _options;

    public AuthServiceUserDirectory(HttpClient http, IOptions<AuthServiceOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<UserContact?> GetUserContactAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
            throw new InvalidOperationException("AuthService:BaseUrl is not configured.");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{_options.BaseUrl.TrimEnd('/')}/auth/admin/users/{userId}");
        if (!string.IsNullOrWhiteSpace(_options.ServiceAdminToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ServiceAdminToken);

        using var response = await _http.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        // 401/403/5xx: problem u sistemu, ne u korisniku - poruka ce se ponoviti.
        response.EnsureSuccessStatusCode();

        var user = await response.Content.ReadFromJsonAsync<AuthUserDto>(cancellationToken);
        if (user is null || user.Id == Guid.Empty || string.IsNullOrWhiteSpace(user.Email))
            return null;

        return new UserContact(user.Id, user.Email, $"{user.FirstName} {user.LastName}".Trim());
    }
}

public sealed class CoursesServiceDirectory : ICourseDirectory
{
    private sealed record CourseDto(Guid Id, string? Name);

    private readonly HttpClient _http;
    private readonly CoursesServiceOptions _options;
    private readonly ILogger<CoursesServiceDirectory> _logger;

    public CoursesServiceDirectory(HttpClient http, IOptions<CoursesServiceOptions> options, ILogger<CoursesServiceDirectory> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string?> GetCourseNameAsync(Guid courseId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
            return null;

        try
        {
            var courses = await _http.GetFromJsonAsync<List<CourseDto>>(
                $"{_options.BaseUrl.TrimEnd('/')}/courses", cancellationToken);
            return courses?.FirstOrDefault(c => c.Id == courseId)?.Name;
        }
        catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(e, "Course name for {CourseId} is not available.", courseId);
            return null;
        }
    }
}
