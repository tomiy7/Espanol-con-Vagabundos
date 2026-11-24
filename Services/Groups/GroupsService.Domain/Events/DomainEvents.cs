namespace GroupsService.Domain.Events;

// Akumuliraju se u aggregate.DomainEvents. Pri snimanju GroupDbContext deo njih prevodi
// u javne (integracione) dogadjaje i upisuje u outbox u istoj transakciji (vidi
// Infrastructure/Outbox/IntegrationEventMapper), a OutboxDispatcher ih salje na RabbitMQ.
// Ostali su interni i ne izlaze iz servisa.

public record EnrollmentCreated(Guid EnrollmentId, Guid UserId, Guid CourseId, string Type);

public record PartnerInvited(Guid InvitationId, Guid InviterEnrollmentId, Guid InviteeUserId);

public record InvitationAccepted(Guid InvitationId, Guid GroupId);

public record InvitationDeclined(Guid InvitationId);

public record GroupMemberJoined(Guid GroupId, Guid EnrollmentId);

public record GroupMemberRemoved(Guid GroupId, Guid EnrollmentId);

public record GroupBecameReady(Guid GroupId);

// Ready -> Forming nazad, jer je otkazivanjem clana broj pao ispod MinSize. Admin/
// profesor i dalje moze da forsira Start() sa manje clanova (vidi Group.Start) -
// ovaj event je samo informativni trag da se to desilo, ne sprecava force-start.
public record GroupDroppedBelowMinSize(Guid GroupId, int MemberCount, int MinSize);

public record SlotAssigned(Guid GroupId, int SlotIndex, Guid ProfessorId);

public record GroupStarted(Guid GroupId, Guid CourseId);

public record EnrollmentCompleted(Guid EnrollmentId);

public record GroupCompleted(Guid GroupId);

// Clan grupe u trenutku dogadjaja (upis + korisnik).
public record MemberRef(Guid EnrollmentId, Guid UserId);

// FormerMembers: ko je bio u grupi pre rasformiranja (grupa ih posle vise nema).
public record GroupDissolved(Guid GroupId, IReadOnlyList<MemberRef> FormerMembers);

public record EnrollmentCancelled(Guid EnrollmentId);

public record EnrollmentFrozen(Guid EnrollmentId, Guid ApprovedByUserId);

public record EnrollmentUnfrozen(Guid EnrollmentId);

// Grupa je pala ispod minimuma, pa upis vise nije Ready (ceka novog clana).
public record EnrollmentReturnedToSearching(Guid EnrollmentId);