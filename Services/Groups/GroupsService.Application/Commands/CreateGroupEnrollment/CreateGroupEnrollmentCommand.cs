namespace GroupsService.Application.Commands.CreateGroupEnrollment;

public record CreateGroupEnrollmentCommand(Guid UserId, Guid CourseId);

public record CreateGroupEnrollmentResult(Guid EnrollmentId, Guid GroupId);