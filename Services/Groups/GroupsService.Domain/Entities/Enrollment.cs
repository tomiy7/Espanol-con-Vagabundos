using GroupsService.Domain.Enums;
using GroupsService.Domain.Events;
using GroupsService.Domain.Exceptions;
using GroupsService.Domain.ValueObjects;

namespace GroupsService.Domain.Entities;

// Centralni entitet: jedan Enrollment = jedan korisnikov upis na jedan kurs,
// bez obzira na tip (Individual/Pair/Group). Group je samo koordinacioni
// mehanizam koji Pair i Group tip KORISTE - Enrollment ostaje vlasnik upisa.
public class Enrollment : AggregateRoot
{
    public Guid UserId { get; private set; }
    public Guid CourseId { get; private set; }
    public EnrollmentType Type { get; private set; }
    public EnrollmentStatus Status { get; private set; }

    // Samo za Individual - IZABRANI profesor (bira ga korisnik pri kupovini, nema
    // rotacije). To je zelja, ne potvrda: stvarni termin i profesor se potvrdjuju
    // preko slota privatne Group. Za Pair/Group ostaje null - profesori za te tipove
    // zive na Group.Slots.
    public Guid? ProfessorId { get; private set; }

    // Postavlja se kad Enrollment udje u Group - SVI tipovi, uklj. Individual
    // (dobija privatnu Group sa 1 clanom i 1 slotom, vidi Group.CreateIndividual).
    public Guid? GroupId { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime? ActivatedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    // Zamrzavanje zaustavlja "sat" za otvaranje lekcija. FrozenAt je trenutak
    // pocetka tekuceg zamrzavanja (null ako nije zamrznut), a TotalFrozenTime je
    // zbir svih ranijih, zavrsenih zamrzavanja.
    public DateTime? FrozenAt { get; private set; }
    public TimeSpan TotalFrozenTime { get; private set; }

    // Napredak po lekcijama vodi Courses (po upisu). Ovde se cuva samo cinjenica da je
    // kurs zavrsen (Status Completed + CompletedAt), koju Courses javlja CourseCompleted
    // event-om (vidi CourseCompletedHandler).

    private Enrollment() { }

    // NAPOMENA: Individual NE krece odmah - profesor (fiksan, bez rotacije, biran
    // ovde pri kreiranju) mora prvo da potvrdi termin (dan/vreme) kroz isti
    // AssignProfessorToSlot mehanizam koji koriste Pair/Group, pre nego sto
    // Enrollment postane Active. Vidi Group.CreateIndividual.
    public static Enrollment CreateIndividual(Guid userId, Guid courseId, Guid professorId)
    {
        ValidateCommon(userId, courseId);

        if (professorId == Guid.Empty)
            throw new GroupsDomainException("Professor ID is required for individual enrollment.");

        var enrollment = new Enrollment
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CourseId = courseId,
            Type = EnrollmentType.Individual,
            ProfessorId = professorId,
            // Searching ovde ne znaci "trazi partnera/grupu" (nema koga da trazi za
            // Individual) - znaci "ceka da profesor potvrdi termin". Isti status kao
            // Pair/Group iz istog razloga: handler-i (MarkReady/MarkActive) ne treba
            // da razlikuju tipove.
            Status = EnrollmentStatus.Searching,
            CreatedAt = DateTime.UtcNow
        };

        enrollment.AddDomainEvent(new EnrollmentCreated(enrollment.Id, userId, courseId, "Individual"));
        return enrollment;
    }

    // Koristi se i za Pair (inicijator, pre prihvatanja partnera) i za Group tip.
    public static Enrollment CreateSearching(Guid userId, Guid courseId, EnrollmentType type)
    {
        ValidateCommon(userId, courseId);

        if (type == EnrollmentType.Individual)
            throw new GroupsDomainException("Use CreateIndividual for individual enrollments.");

        var enrollment = new Enrollment
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CourseId = courseId,
            Type = type,
            Status = EnrollmentStatus.Searching,
            CreatedAt = DateTime.UtcNow
        };

        enrollment.AddDomainEvent(new EnrollmentCreated(enrollment.Id, userId, courseId, type.ToString()));
        return enrollment;
    }

    private static void ValidateCommon(Guid userId, Guid courseId)
    {
        if (userId == Guid.Empty)
            throw new GroupsDomainException("User ID cannot be empty.");

        if (courseId == Guid.Empty)
            throw new GroupsDomainException("Course ID cannot be empty.");
    }

    // Sva tri tipa (uklj. Individual) prolaze kroz Group kao koordinacioni mehanizam -
    // Individual dobija svoju privatnu Group (MinSize=MaxSize=1), vidi Group.CreateIndividual.
    public void AssignToGroup(Guid groupId)
    {
        if (Status != EnrollmentStatus.Searching)
            throw new GroupsDomainException($"Cannot assign enrollment in status {Status} to a group.");

        GroupId = groupId;
    }

    // Poziva ga handler kad Group pređe u Ready (dosegnut minimum clanova)
    public void MarkReady()
    {
        if (Status != EnrollmentStatus.Searching)
            throw new GroupsDomainException($"Cannot mark enrollment as Ready from status {Status}.");

        Status = EnrollmentStatus.Ready;
    }

    // Poziva ga handler kad Group pređe u Active (svi slotovi popunjeni, Start() pozvan)
    public void MarkActive(DateTime? now = null)
    {
        if (Status is not (EnrollmentStatus.Ready or EnrollmentStatus.Searching))
            throw new GroupsDomainException($"Cannot activate enrollment from status {Status}.");

        Status = EnrollmentStatus.Active;
        ActivatedAt = now ?? DateTime.UtcNow;
    }

    // Grupa je pala ispod minimuma (clan otisao) pa upis vise nije "Ready".
    // Invarijanta: Enrollment.Ready <=> Group.Ready.
    public void RevertToSearching()
    {
        if (Status != EnrollmentStatus.Ready)
            throw new GroupsDomainException($"Cannot revert enrollment in status {Status} to Searching.");

        Status = EnrollmentStatus.Searching;
        AddDomainEvent(new EnrollmentReturnedToSearching(Id));
    }

    // Grupa je rasformirana (npr. Pair kad partner otkaze pre starta) - upis ostaje
    // ziv, ali bez grupe i ponovo u Searching, pa moze da pozove novog partnera.
    public void LeaveGroup()
    {
        if (Status is not (EnrollmentStatus.Searching or EnrollmentStatus.Ready))
            throw new GroupsDomainException($"Cannot leave a group from status {Status}.");

        GroupId = null;
        Status = EnrollmentStatus.Searching;
    }

    public void Complete()
    {
        if (Status != EnrollmentStatus.Active)
            throw new GroupsDomainException("Only active enrollments can be completed.");

        Status = EnrollmentStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        AddDomainEvent(new EnrollmentCompleted(Id));
    }

    // Napomena: ovde se NE racuna penal - to je Payments-ova odgovornost (faza 2),
    // konfigurabilno po danu/statusu. Domain samo belezi da je otkazano i kada.
    public void Cancel()
    {
        if (Status is EnrollmentStatus.Cancelled or EnrollmentStatus.Completed)
            throw new GroupsDomainException($"Cannot cancel enrollment in status {Status}.");

        // Otkazan upis vise ne pripada grupi (handler ga skida iz Group.Members), pa
        // GroupId ostaje prazan - inace bi GetByGroupIdAsync vracao i otkazane upise.
        // Ako je bio zamrznut, tekuce zamrzavanje se zatvara.
        CloseFreezePeriod(DateTime.UtcNow);
        GroupId = null;
        Status = EnrollmentStatus.Cancelled;
        CancelledAt = DateTime.UtcNow;
        AddDomainEvent(new EnrollmentCancelled(Id));
    }

    public void Freeze(Guid approvedByUserId, DateTime? now = null)
    {
        if (approvedByUserId == Guid.Empty)
            throw new GroupsDomainException("Freeze must be approved by someone (admin or professor).");

        if (Status != EnrollmentStatus.Active)
            throw new GroupsDomainException("Only active enrollments can be frozen.");

        Status = EnrollmentStatus.Frozen;
        FrozenAt = now ?? DateTime.UtcNow;
        AddDomainEvent(new EnrollmentFrozen(Id, approvedByUserId));
    }

    public void Unfreeze(DateTime? now = null)
    {
        if (Status != EnrollmentStatus.Frozen)
            throw new GroupsDomainException("Only frozen enrollments can be unfrozen.");

        CloseFreezePeriod(now ?? DateTime.UtcNow);
        Status = EnrollmentStatus.Active;
        AddDomainEvent(new EnrollmentUnfrozen(Id));
    }

    private void CloseFreezePeriod(DateTime now)
    {
        if (!FrozenAt.HasValue) return;

        var frozenFor = now - FrozenAt.Value;
        if (frozenFor > TimeSpan.Zero)
            TotalFrozenTime += frozenFor;

        FrozenAt = null;
    }

    // Zamrzavanje je vremenski ograniceno (npr. 30 dana) - posle toga profesor/admin
    // odlucuje: odmrznuti ili otkazati. Pozadinski posao koji ovo proverava je planiran.
    public bool IsFreezeExpired(int maxFreezeDays, DateTime? now = null) =>
        Status == EnrollmentStatus.Frozen
        && FrozenAt.HasValue
        && (now ?? DateTime.UtcNow) - FrozenAt.Value > TimeSpan.FromDays(maxFreezeDays);

    // Koliko je lekcija otvoreno: lessonsPerWeek po proteloj nedelji, racunato od
    // unlockStartsAt (grupe), bez vremena provedenog u zamrzavanju. Prve lekcije se
    // otvaraju odmah cim sat krene. Gornju granicu (ukupan broj lekcija) primenjuje
    // Courses, koji zna koliko lekcija kurs ima. Pojednostavljenje: zamrzavanje pre
    // unlockStartsAt se takodje oduzima.
    public int UnlockedLessons(DateTime unlockStartsAt, int lessonsPerWeek, DateTime? now = null)
    {
        if (lessonsPerWeek <= 0)
            throw new GroupsDomainException("Lessons per week must be positive.");

        var current = now ?? DateTime.UtcNow;
        var elapsed = current - unlockStartsAt - TotalFrozenTime;
        if (FrozenAt.HasValue)
            elapsed -= current - FrozenAt.Value;

        if (elapsed < TimeSpan.Zero)
            return 0;

        var fullWeeks = (int)(elapsed.TotalDays / 7);
        return lessonsPerWeek * (fullWeeks + 1);
    }

    // Pristup kursu: svi tipovi (uklj. Individual) tek kad njihova Group predje u
    // Active, tj. profesor potvrdi termin i grupa se pokrene (Enrollment.Status
    // postaje Active tad). Zamrznut/zavrsen/otkazan upis nema pristup.
    public bool GrantsAccess() => Status == EnrollmentStatus.Active;
}