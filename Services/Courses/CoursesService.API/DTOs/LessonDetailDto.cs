using System;
using System.Collections.Generic;

namespace CoursesService.API.DTOs;

public class LessonDetailDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Order { get; set; }
    public string? RecordingUrl { get; set; }
    public List<SectionDto> Sections { get; set; } = new();
    public List<TaskDto> Tasks { get; set; } = new();
}

public class SectionDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }
    public int Order { get; set; }
}

public class TaskDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public decimal Points { get; set; }
    public int Order { get; set; }
}