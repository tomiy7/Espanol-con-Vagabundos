namespace GroupsService.Domain.Events;

// Akumuliraju se u aggregate.DomainEvents ali se NIGDE automatski ne publish-uju.
// Mejlovi se salju eksplicitno iz Application handlera (INotificationService ->
// IEmailSender -> RabbitMQ -> NotificationsService), ne preko ovih event-a.
// Messaging projekat i RabbitMQ vec postoje, pa je sledeci korak dispatcher koji
// posle SaveChanges objavljuje ove event-e preko IEventPublisher-a (uz outbox),
// umesto direktnih poziva iz handlera.

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

public record EnrollmentCancelled(Guid EnrollmentId);

public record EnrollmentFrozen(Guid EnrollmentId, Guid ApprovedByUserId);