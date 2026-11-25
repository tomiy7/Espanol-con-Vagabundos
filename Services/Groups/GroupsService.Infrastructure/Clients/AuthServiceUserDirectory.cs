using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroupsService.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GroupsService.Infrastructure.Clients;

public class AuthServiceUserDirectory : IUserDirectory
{
    // Oblik odgovora preuzet TACNO iz AuthService.API.DTOs.UserDetailResponseDto -
    // ako se taj DTO promeni, ovo mora da prati izmenu.
    private record AuthUserDetailDto(
        Guid Id, string Username, string Email, string Phone,
        string FirstName, string LastName, string Role, DateTime CreatedAt);
    
    private readonly HttpClient _httpClient;
    private readonly AuthServiceOptions _options;
    private readonly ILogger<AuthServiceUserDirectory> _logger;
    
    public AuthServiceUserDirectory(
        HttpClient httpClient, IOptions<AuthServiceOptions> options, ILogger<AuthServiceUserDirectory> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }
    
    public async Task<UserContact?> GetUserContactAsync(Guid userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _logger.LogWarning("AuthService:BaseUrl not configured - can't read user info for {UserId}.", userId);
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get, $"{_options.BaseUrl.TrimEnd('/')}/auth/admin/users/{userId}");

            if (!string.IsNullOrWhiteSpace(_options.ServiceAdminToken))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ServiceAdminToken);

            using var response = await _httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Auth service response {StatusCode} for user {UserId}.", response.StatusCode, userId);
                return null;
            }

            var dto = await response.Content.ReadFromJsonAsync<AuthUserDetailDto>(cancellationToken: ct);
            // Odgovor koji nije ocekivanog oblika (nema id-ja ili mejla) = nema podataka,
            // umesto praznog kontakta na koji bi se kasnije slao mejl.
            if (dto is null || dto.Id == Guid.Empty || string.IsNullOrWhiteSpace(dto.Email))
            {
                _logger.LogWarning("Auth service returned an unexpected user payload for {UserId}.", userId);
                return null;
            }

            return new UserContact(dto.Id, dto.Email, $"{dto.FirstName} {dto.LastName}".Trim());
        }
        catch (Exception ex)
        {
            // Isto pravilo kao kod mejla: nedostupnost Auth servisa ne sme da obori
            // domensku operaciju. Pozivalac dobija null i odlucuje sam sta dalje
            // (npr. preskace slanje mejla, loguje, i nastavlja).
            _logger.LogError(ex, "Request to Auth sevice for user {UserId} failed.", userId);
            return null;
        }
    }
}