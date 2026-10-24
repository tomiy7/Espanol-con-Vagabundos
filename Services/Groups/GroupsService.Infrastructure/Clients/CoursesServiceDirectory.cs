using System.Net.Http.Json;
using GroupsService.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GroupsService.Infrastructure.Clients;

public class CoursesServiceDirectory : ICourseDirectory
{
    // Oblik preuzet tacno iz CoursesService.API.DTOs.CourseListItemDto
    private record CourseListItemDto(
        Guid Id, string Name, string Slug, string? Description,
        decimal Price, Guid? Thumbnail, string? Level, int? DurationWeeks);

    private readonly HttpClient _httpClient;
    private readonly CoursesServiceOptions _options;
    private readonly ILogger<CoursesServiceDirectory> _logger;

    public CoursesServiceDirectory(
        HttpClient httpClient, IOptions<CoursesServiceOptions> options, ILogger<CoursesServiceDirectory> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string?> GetCourseNameAsync(Guid courseId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _logger.LogWarning("CoursesService:BaseUrl not configured - can't load the name of the course {CourseId}.", courseId);
            return null;
        }

        try
        {
            var courses = await _httpClient.GetFromJsonAsync<List<CourseListItemDto>>(
                $"{_options.BaseUrl.TrimEnd('/')}/courses", ct);

            return courses?.FirstOrDefault(c => c.Id == courseId)?.Name;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Request for Courses service for course {CourseId} failed.", courseId);
            return null;
        }
    }
}