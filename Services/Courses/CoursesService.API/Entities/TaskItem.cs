using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoursesService.API.Entities;

[Table("tasks")]
public class TaskItem
{
    [Column("id")]
    public Guid Id { get; set; }

    [Column("date_created")]
    public DateTime? DateCreated { get; set; }

    [Column("date_updated")]
    public DateTime? DateUpdated { get; set; }

    [Column("lesson_id")]
    public Guid LessonId { get; set; }

    [Column("question", TypeName = "jsonb")]
    public string Question { get; set; } = string.Empty;

    [Column("type")]
    [MaxLength(20)]
    public string Type { get; set; } = string.Empty;

    [Column("correct_answer")]
    public string? CorrectAnswer { get; set; }

    [Column("points")]
    public decimal Points { get; set; }

    [Column("order")]
    public int Order { get; set; }

    [ForeignKey(nameof(LessonId))]
    public Lesson Lesson { get; set; } = null!;
}