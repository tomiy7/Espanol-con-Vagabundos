namespace Messaging.Contracts;

// Dogadjaji koje objavljuje Groups (preko outbox-a, pa se ne gube i ne objavljuju se za
// promenu koja nije snimljena). Nose dovoljno podataka da potrosac (Payments,
// Notifications) ne mora odmah da zove Groups API.
// Ugovori se menjaju samo dodavanjem polja - postojeci potrosaci moraju da rade i dalje.

// Clan grupe: upis i korisnik (kome se salje mejl, ko placa).
// PaymentPlan: "Full" ili "Installments" - izbor placanja sa upisa (za Payments).
public sealed record GroupMemberInfo(Guid EnrollmentId, Guid UserId)
{
    public string PaymentPlan { get; init; } = "Full";
}

// Termin u nedelji (npr. Monday 18:00).
public sealed record GroupSlotInfo(DayOfWeek DayOfWeek, TimeOnly Time);

// Grupa je popunjena (dostigla minimum clanova) - kandidat za generisanje uplatnica.
public sealed record GroupBecameReady(
    Guid GroupId, Guid CourseId, string GroupType, IReadOnlyList<Guid> MemberEnrollmentIds) : IntegrationEvent
{
    public IReadOnlyList<GroupMemberInfo> Members { get; init; } = [];
}

// Grupa je pocela; od UnlockStartsAt se broje nedelje za otvaranje lekcija.
public sealed record GroupStarted(
    Guid GroupId, Guid CourseId, DateTime StartedAt, DateTime UnlockStartsAt) : IntegrationEvent
{
    public string GroupType { get; init; } = string.Empty;
    public IReadOnlyList<GroupMemberInfo> Members { get; init; } = [];
    public IReadOnlyList<GroupSlotInfo> Slots { get; init; } = [];
}

// Grupa je rasformirana pre pocetka (uplate, ako ih ima, resava osoblje).
// Members: ko je bio u grupi u trenutku rasformiranja (npr. partner koji je ostao).
public sealed record GroupDissolved(Guid GroupId, Guid CourseId) : IntegrationEvent
{
    public string GroupType { get; init; } = string.Empty;
    public IReadOnlyList<GroupMemberInfo> Members { get; init; } = [];
}

public sealed record GroupCompleted(Guid GroupId, Guid CourseId) : IntegrationEvent
{
    public IReadOnlyList<GroupMemberInfo> Members { get; init; } = [];
}

// Novi upis (Individual, Group ili Pair). GroupId je prazan dok upis nije u grupi.
public sealed record EnrollmentCreated(
    Guid EnrollmentId, Guid UserId, Guid CourseId, string EnrollmentType, Guid? GroupId) : IntegrationEvent;

public sealed record EnrollmentCancelled(Guid EnrollmentId, Guid UserId, Guid CourseId) : IntegrationEvent;

public sealed record EnrollmentCompleted(Guid EnrollmentId, Guid UserId, Guid CourseId) : IntegrationEvent;

public sealed record EnrollmentFrozen(Guid EnrollmentId, Guid UserId, Guid CourseId) : IntegrationEvent;

public sealed record EnrollmentUnfrozen(Guid EnrollmentId, Guid UserId, Guid CourseId) : IntegrationEvent;

// Grupa je pala ispod minimuma (neko otkazao), pa upis ponovo ceka da se grupa popuni.
public sealed record EnrollmentReturnedToSearching(Guid EnrollmentId, Guid UserId, Guid CourseId) : IntegrationEvent;

// Poziv za ucenje u paru. Token je jednokratan kljuc iz linka u mejlu - poruka zato
// ide samo kroz interni broker i ne loguje se.
public sealed record PartnerInvited(
    Guid InvitationId, Guid InviterUserId, Guid InviteeUserId, Guid CourseId, string Token, DateTime ExpiresAt)
    : IntegrationEvent;

public sealed record InvitationAccepted(
    Guid InvitationId, Guid GroupId, Guid InviterUserId, Guid InviteeUserId, Guid CourseId) : IntegrationEvent;

public sealed record InvitationDeclined(
    Guid InvitationId, Guid InviterUserId, Guid InviteeUserId, Guid CourseId) : IntegrationEvent;

// Grupa prvi put ima i dovoljno clanova i planirani datum prvog casa (desava se jednom).
// Payments izdaje uplatnice clanovima; rokovi se racunaju od FirstLessonAt.
public sealed record GroupConfirmed(
    Guid GroupId, Guid CourseId, string GroupType, DateTime FirstLessonAt,
    IReadOnlyList<GroupMemberInfo> Members) : IntegrationEvent;

// Clan je usao u vec potvrdjenu grupu - njemu treba uplatnica.
public sealed record MemberJoinedConfirmedGroup(
    Guid GroupId, Guid CourseId, string GroupType, DateTime FirstLessonAt,
    GroupMemberInfo Member) : IntegrationEvent;

// Planirani datum prvog casa je pomeren (rokovi neplacenih rata se pomeraju, ucenici dobijaju mejl).
public sealed record FirstLessonRescheduled(
    Guid GroupId, Guid CourseId, DateTime OldFirstLessonAt, DateTime NewFirstLessonAt,
    IReadOnlyList<GroupMemberInfo> Members) : IntegrationEvent;
