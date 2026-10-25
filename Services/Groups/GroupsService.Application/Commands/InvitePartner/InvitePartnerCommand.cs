namespace GroupsService.Application.Commands.InvitePartner;

public record InvitePartnerCommand(Guid InviterUserId, Guid InviteeUserId, Guid CourseId);

public record InvitePartnerResult(Guid EnrollmentId, Guid InvitationId);