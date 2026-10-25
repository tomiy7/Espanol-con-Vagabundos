namespace GroupsService.Application.Commands.CancelEnrollment;

public record CancelEnrollmentCommand(Guid EnrollmentId, Guid RequestingUserId);