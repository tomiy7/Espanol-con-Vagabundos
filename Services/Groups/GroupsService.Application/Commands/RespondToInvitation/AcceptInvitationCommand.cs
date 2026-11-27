using GroupsService.Domain.Enums;

namespace GroupsService.Application.Commands.RespondToInvitation;

// PaymentPlan: izbor placanja pozvanog (null = odjednom).
public record AcceptInvitationCommand(string Token, PaymentPlan? PaymentPlan = null);

public record AcceptInvitationResult(Guid GroupId, Guid InviteeEnrollmentId);