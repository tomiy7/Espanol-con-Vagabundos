using System.Text.Json;
using CoursesService.API.Access;
using CoursesService.API.DTOs;
using CoursesService.API.Entities;
using CoursesService.API.Errors;
using CoursesService.API.Options;
using CoursesService.API.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CoursesService.API.Controllers;

// Katalog je javan (naziv, opis, cene, naslovi lekcija); sadrzaj lekcije samo za upisane
// (po rasporedu otkljucavanja iz Groups-a) i za osoblje.
[Route("courses")]
public class CoursesController : ApiControllerBase
{
    private readonly ICourseRepository _courseRepository;
    private readonly IGroupsAccessClient _groupsAccess;
    private readonly PricingOptions _pricing;
    private readonly ILogger<CoursesController> _logger;

    public CoursesController(
        ICourseRepository repo,
        IGroupsAccessClient groupsAccess,
        IOptions<PricingOptions> pricing,
        ILogger<CoursesController> logger)
    {
        _courseRepository = repo;
        _groupsAccess = groupsAccess;
        _pricing = pricing.Value;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<CourseListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllPublished(CancellationToken cancellationToken)
    {
        var courses = await _courseRepository.GetPublishedCoursesAsync(cancellationToken);
        var response = courses.Select(c => new CourseListItemDto
        {
            Id = c.Id,
            Name = c.Name,
            Slug = c.Slug,
            Description = c.Description,
            Price = c.Price,
            Prices = PricesDto.From(c),
            Thumbnail = c.Thumbnail,
            Level = c.Level,
            DurationWeeks = c.DurationWeeks
        }).ToList();

        return Ok(response);
    }

    // Cene za Payments (i za kurs koji vise nije u ponudi - grupa moze jos da traje).
    [HttpGet("{courseId:guid}/pricing")]
    [ProducesResponseType(typeof(CoursePricingDto), StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPricing(Guid courseId, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.GetByIdAsync(courseId, cancellationToken);
        if (course == null)
            return ProblemWithCode(StatusCodes.Status404NotFound, ErrorCodes.CourseNotFound, "Course does not exist.");

        return Ok(new CoursePricingDto(course.Id, _pricing.Currency,
            course.PriceIndividual, course.PricePair, course.PriceGroup, course.EbookPrice));
    }

    // Front otvara kurseve po id-u; slug ostaje za lepe adrese.
    [HttpGet("{courseId:guid}")]
    [ProducesResponseType(typeof(CourseDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid courseId, CancellationToken cancellationToken) =>
        Detail(await _courseRepository.GetPublishedByIdWithLessonsAsync(courseId, cancellationToken));

    [HttpGet("{slug}")]
    [ProducesResponseType(typeof(CourseDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken cancellationToken) =>
        Detail(await _courseRepository.GetPublishedBySlugWithLessonsAsync(slug, cancellationToken));

    private IActionResult Detail(Course? course)
    {
        if (course == null)
            return ProblemWithCode(StatusCodes.Status404NotFound, ErrorCodes.CourseNotFound, "Course does not exist.");

        // ID PDF-a e-knjige se namerno ne vraca: to je placeni sadrzaj.
        var response = new CourseDetailDto
        {
            Id = course.Id,
            Name = course.Name,
            Slug = course.Slug,
            Description = course.Description,
            Price = course.Price,
            EbookPrice = course.EbookPrice,
            Prices = PricesDto.From(course),
            Thumbnail = course.Thumbnail,
            Level = course.Level,
            DurationWeeks = course.DurationWeeks,
            Lessons = course.Lessons.OrderBy(l => l.Order).ThenBy(l => l.Id).Select(l => new LessonListItemDto
            {
                Id = l.Id,
                Title = l.Title,
                Order = l.Order,
                RecordingUrlDate = l.RecordingUrlDate
            }).ToList()
        };

        return Ok(response);
    }

    // Osoblje vidi svaku objavljenu lekciju (i kursa koji se jos priprema). Ucenik samo
    // lekcije objavljenog kursa na koji ima aktivan upis, do broja otkljucanih (Groups).
    [HttpGet("lessons/{lessonId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(LessonDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetLesson(Guid lessonId, CancellationToken cancellationToken)
    {
        var lesson = await _courseRepository.GetLessonAsync(lessonId, cancellationToken);
        if (lesson == null || (!IsStaff && !lesson.Course.IsPublished))
            return ProblemWithCode(StatusCodes.Status404NotFound, ErrorCodes.LessonNotFound, "Lesson does not exist.");

        if (!IsStaff)
        {
            var denied = await CheckStudentAccess(lesson, cancellationToken);
            if (denied != null)
                return denied;
        }

        return Ok(ToDto(lesson));
    }

    private async Task<IActionResult?> CheckStudentAccess(Lesson lesson, CancellationToken cancellationToken)
    {
        var token = await HttpContext.GetTokenAsync("access_token") ?? string.Empty;

        CourseAccess access;
        try
        {
            access = await _groupsAccess.CheckAsync(token, lesson.CourseId, cancellationToken);
        }
        catch (GroupsRejectedTokenException)
        {
            return ProblemWithCode(StatusCodes.Status401Unauthorized, ErrorCodes.Unauthorized, "Authentication is required.");
        }
        catch (GroupsUnavailableException e)
        {
            _logger.LogWarning(e, "Access check for lesson {LessonId} failed: Groups is unavailable.", lesson.Id);
            return ProblemWithCode(StatusCodes.Status503ServiceUnavailable, ErrorCodes.AccessCheckUnavailable,
                "Access could not be checked right now. Try again later.");
        }

        if (!access.HasAccess)
            return ProblemWithCode(StatusCodes.Status403Forbidden, ErrorCodes.NoCourseAccess,
                "You do not have an active enrollment for this course.");

        var lessons = await _courseRepository.GetLessonOrderAsync(lesson.CourseId, cancellationToken);
        if (LessonPosition.Of(lessons, lesson.Id) > access.UnlockedLessons)
            return ProblemWithCode(StatusCodes.Status403Forbidden, ErrorCodes.LessonLocked, "This lesson is not unlocked yet.");

        return null;
    }

    private static LessonDetailDto ToDto(Lesson lesson) => new()
    {
        Id = lesson.Id,
        CourseId = lesson.CourseId,
        Title = lesson.Title,
        Order = lesson.Order,
        RecordingUrl = lesson.RecordingUrl,
        Sections = lesson.Sections.OrderBy(s => s.Order).ThenBy(s => s.Id).Select(s => new SectionDto
        {
            Id = s.Id,
            Title = s.Title,
            Content = ParseJson(s.Content),
            Order = s.Order
        }).ToList(),
        Tasks = lesson.Tasks.OrderBy(t => t.Order).ThenBy(t => t.Id).Select(t => new TaskDto
        {
            Id = t.Id,
            Type = t.Type,
            Question = t.Question,
            Points = t.Points,
            Order = t.Order
        }).ToList()
    };

    private static object? ParseJson(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return raw;
        }
    }
}
