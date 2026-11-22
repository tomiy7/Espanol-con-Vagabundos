namespace GroupsService.Application.Commands.RespondToInvitation;

public record AcceptInvitationCommand(string Token);

public record AcceptInvitationResult(Guid GroupId, Guid InviteeEnrollmentId);