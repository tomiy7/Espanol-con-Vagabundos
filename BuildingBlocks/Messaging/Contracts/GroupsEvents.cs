namespace Messaging.Contracts;

// Dogadjaji koje objavljuje Groups (preko outbox-a, pa se ne gube i ne objavljuju se za
// promenu koja nije snimljena). Nose dovoljno podataka da potrosac (Payments,
// Notifications) ne mora odmah da zove Groups API.

// Grupa je popunjena (dostigla minimum clanova) - kandidat za generisanje uplatnica.
public sealed record GroupBecameReady(
    Guid GroupId, Guid CourseId, string GroupType, IReadOnlyList<Guid> MemberEnrollmentIds) : IntegrationEvent;

// Grupa je pocela; od UnlockStartsAt se broje nedelje za otvaranje lekcija.
public sealed record GroupStarted(
    Guid GroupId, Guid CourseId, DateTime StartedAt, DateTime UnlockStartsAt) : IntegrationEvent;

// Grupa je rasformirana pre pocetka (uplate, ako ih ima, resava osoblje).
public sealed record GroupDissolved(Guid GroupId, Guid CourseId) : IntegrationEvent;

public sealed record GroupCompleted(Guid GroupId, Guid CourseId) : IntegrationEvent;

public sealed record EnrollmentCancelled(Guid EnrollmentId, Guid UserId, Guid CourseId) : IntegrationEvent;

public sealed record EnrollmentCompleted(Guid EnrollmentId, Guid UserId, Guid CourseId) : IntegrationEvent;
