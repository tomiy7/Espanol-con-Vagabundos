namespace GroupsService.Application.Commands.JoinGroup;

// UserId je ucenik koji ulazi u grupu. AddedByStaff=true kad ga ubacuje profesor/admin
// (ne gleda se rok formiranja); false kad se ucenik sam prijavljuje na izabranu grupu.
public record JoinGroupCommand(Guid UserId, Guid GroupId, bool AddedByStaff = false);

public record JoinGroupResult(Guid EnrollmentId, Guid GroupId);
