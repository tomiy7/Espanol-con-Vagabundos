using System;

namespace CoursesService.API.DTOs;

public class CourseListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public float Price { get; set; }
    public Guid? Thumbnail { get; set; }
    public string? Level { get; set; }
    public int? DurationWeeks { get; set; }
}