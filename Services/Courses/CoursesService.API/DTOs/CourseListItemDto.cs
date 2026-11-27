using System;

namespace CoursesService.API.DTOs;

public class CourseListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    // Stara jedinstvena cena - ostaje dok front ne predje na Prices.
    public decimal Price { get; set; }
    public PricesDto Prices { get; set; } = null!;
    public Guid? Thumbnail { get; set; }
    public string? Level { get; set; }
    public int? DurationWeeks { get; set; }
}