using System;

namespace CoursesService.API.DTOs;

public class LessonListItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Order { get; set; }
    public DateTime? RecordingUrlDate { get; set; }
}