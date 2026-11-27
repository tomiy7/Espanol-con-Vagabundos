using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoursesService.API.Options;
using Microsoft.Extensions.Options;

namespace CoursesService.API.Access;

// Odgovor Groups-a: da li korisnik ima aktivan upis na kurs i koliko je lekcija otkljucano.
public sealed record CourseAccess(bool HasAccess, Guid? EnrollmentId, int UnlockedLessons);

// Groups nije odgovorio ili je odgovorio neocekivano - sadrzaj se ne pusta "na slepo".
public sealed class GroupsUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

// Groups je odbio korisnikov token (npr. istekao) - za klijenta je to 401.
public sealed class GroupsRejectedTokenException() : Exception("Groups rejected the user token.");

public interface IGroupsAccessClient
{
    Task<CourseAccess> CheckAsync(string bearerToken, Guid courseId, CancellationToken cancellationToken);
}

// Pita Groups sa KORISNIKOVIM tokenom (GET /enrollments/access), pa Groups sam cita ko pita;
// Courses ne salje userId i ne treba mu servisni token.
public sealed class GroupsAccessClient(HttpClient http, IOptions<GroupsServiceOptions> options) : IGroupsAccessClient
{
    public async Task<CourseAccess> CheckAsync(string bearerToken, Guid courseId, CancellationToken cancellationToken)
    {
        var url = $"{options.Value.BaseUrl.TrimEnd('/')}/enrollments/access?courseId={courseId}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new GroupsUnavailableException("Groups service did not respond.", e);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new GroupsRejectedTokenException();

            if (!response.IsSuccessStatusCode)
                throw new GroupsUnavailableException($"Groups service returned {(int)response.StatusCode}.");

            try
            {
                return await response.Content.ReadFromJsonAsync<CourseAccess>(cancellationToken)
                    ?? throw new GroupsUnavailableException("Groups service returned an empty body.");
            }
            catch (System.Text.Json.JsonException e)
            {
                throw new GroupsUnavailableException("Groups service returned an unreadable body.", e);
            }
        }
    }
}
