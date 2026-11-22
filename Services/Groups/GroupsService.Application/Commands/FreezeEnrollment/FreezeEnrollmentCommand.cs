namespace GroupsService.Application.Commands.FreezeEnrollment;

public record FreezeEnrollmentCommand(Guid EnrollmentId, Guid ApprovedByUserId);