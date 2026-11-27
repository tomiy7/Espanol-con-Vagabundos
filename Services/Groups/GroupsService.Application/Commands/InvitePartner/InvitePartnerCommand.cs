using GroupsService.Domain.Enums;

namespace GroupsService.Application.Commands.InvitePartner;

// PaymentPlan: izbor placanja inicijatora (null = odjednom, ili ostaje raniji izbor kod ponovnog poziva).
public record InvitePartnerCommand(Guid InviterUserId, Guid InviteeUserId, Guid CourseId, PaymentPlan? PaymentPlan = null);

public record InvitePartnerResult(Guid EnrollmentId, Guid InvitationId);