using GroupsService.Domain.Enums;

namespace GroupsService.Application.Commands.JoinGroup;

// UserId je ucenik koji ulazi u grupu. AddedByStaff=true kad ga ubacuje profesor/admin
// (ne gleda se rok formiranja); false kad se ucenik sam prijavljuje na izabranu grupu.
// PaymentPlan: izbor placanja (null = odjednom).
public record JoinGroupCommand(Guid UserId, Guid GroupId, bool AddedByStaff = false, PaymentPlan? PaymentPlan = null);

public record JoinGroupResult(Guid EnrollmentId, Guid GroupId);
