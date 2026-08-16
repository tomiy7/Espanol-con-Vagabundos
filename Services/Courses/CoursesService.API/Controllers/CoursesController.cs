using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CoursesService.API.DTOs;
using CoursesService.API.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CoursesService.API.Controllers;

[ApiController]
[Route("courses")]
public class CoursesController : ControllerBase
{
    private readonly ICourseRepository _courseRepository;
    private readonly ILogger<CoursesController> _logger;
    
    public CoursesController(ICourseRepository repo, ILogger<CoursesController> logger)
    {
        _courseRepository = repo;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<CourseListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllPublished()
    {
        var courses = await _courseRepository.GetPublishedCoursesAsync();
        var response = courses.Select(c => new CourseListItemDto
        {
            Id = c.Id,
            Name = c.Name,
            Slug = c.Slug,
            Description = c.Description,
            Price = c.Price,
            Thumbnail = c.Thumbnail,
            Level = c.Level,
            DurationWeeks = c.DurationWeeks
        }).ToList();

        return Ok(response);
    }
    
    [HttpGet("{slug}")]
    [ProducesResponseType(typeof(CourseDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBySlug(string slug)
    {
        var course = await _courseRepository.GetBySlugAsync(slug);
        if (course == null)
        {
            _logger.LogWarning("Course not found for slug: {Slug}", slug);
            return NotFound(new { error = "COURSE_NOT_FOUND", message = "Course doesn't exist" });
        }

        var full = await _courseRepository.GetByIdWithLessonsAsync(course.Id);
        var response = new CourseDetailDto
        {
            Id = full!.Id,
            Name = full.Name,
            Slug = full.Slug,
            Description = full.Description,
            Price = full.Price,
            EbookPrice = full.EbookPrice,
            Thumbnail = full.Thumbnail,
            EbookPdf = full.EbookId,
            Level = full.Level,
            DurationWeeks = full.DurationWeeks,
            Lessons = full.Lessons.OrderBy(l => l.Order).Select(l => new LessonListItemDto
            {
                Id = l.Id,
                Title = l.Title,
                Order = l.Order,
                RecordingUrlDate = l.RecordingUrlDate
            }).ToList()
        };

        return Ok(response);
    }
    
    [HttpGet("lessons/{lessonId}")]
    [ProducesResponseType(typeof(LessonDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLesson(Guid lessonId)
    {
        var lesson = await _courseRepository.GetLessonByIdAsync(lessonId);
        if (lesson == null)
        {
            _logger.LogWarning("Lesson not found: {LessonId}", lessonId);
            return NotFound(new { error = "LESSON_NOT_FOUND", message = "Lesson doesn't exist" });
        }

        return Ok(new LessonDetailDto
        {
            Id = lesson.Id,
            Title = lesson.Title,
            Order = lesson.Order,
            RecordingUrl = lesson.RecordingUrl,
            Sections = lesson.Sections.Select(s => new SectionDto
            {
                Id = s.Id,
                Title = s.Title,
                Content = s.Content,
                Order = s.Order
            }).ToList(),
            Tasks = lesson.Tasks.Select(t => new TaskDto
            {
                Id = t.Id,
                Type = t.Type,
                Question = t.Question,
                Points = t.Points,
                Order = t.Order
            }).ToList()
        });
    }
}