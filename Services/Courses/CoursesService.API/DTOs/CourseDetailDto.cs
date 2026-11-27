using System;
using System.Collections.Generic;

namespace CoursesService.API.DTOs;

public class CourseDetailDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal? EbookPrice { get; set; }
    public PricesDto Prices { get; set; } = null!;
    public Guid? Thumbnail { get; set; }
    public string? Level { get; set; }
    public int? DurationWeeks { get; set; }
    public List<LessonListItemDto> Lessons { get; set; } = new();
}