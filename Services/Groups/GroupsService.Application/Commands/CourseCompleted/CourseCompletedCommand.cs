namespace GroupsService.Application.Commands.CourseCompleted;

// Courses javlja da je ucenik zavrsio sve lekcije i zavrsni test za dati upis.
public record CourseCompletedCommand(Guid EnrollmentId, Guid UserId, Guid CourseId);