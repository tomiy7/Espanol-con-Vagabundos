namespace GroupsService.Application.Commands.RecordLessonCompleted;

public record RecordLessonCompletedCommand(
    Guid UserId, Guid CourseId, Guid EnrollmentId, Guid LessonId, int TotalLessons);
