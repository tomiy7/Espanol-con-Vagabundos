using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoursesService.API.Entities;

[Table("courses")]
public class Course
{
    [Column("id")]
    public Guid Id { get; set; }
    
    [Column("name")]
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Column("slug_name")]
    [Required, MaxLength(150)]
    public string Slug { get; set; } = string.Empty;
    
    [Column("description")]
    public string? Description { get; set; }
    
    [Column("is_published")]
    public bool IsPublished { get; set; }
    
    [Column("price")]
    public decimal Price { get; set; }
    
    [Column("ebook_price")]
    public decimal? EbookPrice { get; set; }
    
    [Column("level")]
    [MaxLength(20)]
    public string Level { get; set; } = string.Empty;
    
    [Column("duration_weeks")]
    public int DurationWeeks { get; set; }
    
    [Column("thumbnail")]
    public Guid? Thumbnail { get; set; }
    
    [Column("ebook_pdf")]
    public Guid? EbookId { get; set; }

    [Column("date_created")]
    public DateTime CreatedAt { get; set; } 
    
    [Column("date_updated")]
    public DateTime UpdatedAt { get; set; }

    public List<Lesson> Lessons { get; set; } = new();
}