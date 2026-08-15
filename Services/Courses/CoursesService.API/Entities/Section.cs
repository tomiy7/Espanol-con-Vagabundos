using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoursesService.API.Entities;

[Table("sections")]
public class Section
{
    [Column("id")]
    public Guid Id { get; set; }

    [Column("date_created")]
    public DateTime? DateCreated { get; set; }

    [Column("date_updated")]
    public DateTime? DateUpdated { get; set; }

    [Column("lesson_id")]
    public Guid LessonId { get; set; }

    [Column("title")]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Column("content", TypeName = "jsonb")]
    public string? Content { get; set; }

    [Column("order")]
    public int Order { get; set; }

    [ForeignKey(nameof(LessonId))]
    public Lesson Lesson { get; set; } = null!;
}