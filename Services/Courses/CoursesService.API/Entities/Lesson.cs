using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoursesService.API.Entities;

[Table("lessons")]
public class Lesson
{
    [Column("id")]
    public Guid Id { get; set; }

    [Column("date_created")]
    public DateTime DateCreated { get; set; }

    [Column("date_updated")]
    public DateTime? DateUpdated { get; set; }

    [Column("course_id")]
    public Guid CourseId { get; set; }

    [Column("title")]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Column("order")]
    public int Order { get; set; }

    [Column("recording_url")]
    [MaxLength(500)]
    public string? RecordingUrl { get; set; }

    [Column("recording_url_date")]
    public DateTime? RecordingUrlDate { get; set; }

    [Column("is_visible")]
    public bool IsVisible { get; set; }

    [Column("is_published")]
    public bool IsPublished { get; set; }

    [ForeignKey(nameof(CourseId))]
    public Course Course { get; set; } = null!;

    public List<Section> Sections { get; set; } = new();
    public List<TaskItem> Tasks { get; set; } = new();
}