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
    
    // Samo za Individual - fiksni, bira ga korisnik pri kupovini, nema rotacije.
    // Za Pair/Group ostaje null - profesori za te tipove zive na Group.Slots.
    public Guid? ProfessorId { get; private set; }
    
    // Postavlja se kad Enrollment udje u Group - SVI tipovi, uklj. Individual
    public Guid? GroupId { get; private set; }
    
    public DateTime CreatedAt { get; private set; }
    public DateTime? ActivatedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    
    private readonly List<CompletedLesson> _completedLessons = new();
    public IReadOnlyCollection<CompletedLesson> CompletedLessons => _completedLessons.AsReadOnly();
    public int CompletedLessonsCount => _completedLessons.Count;
    
    private Enrollment() { }

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
    public void MarkActive()
    {
        if (Status is not (EnrollmentStatus.Ready or EnrollmentStatus.Searching))
            throw new GroupsDomainException($"Cannot activate enrollment from status {Status}.");

        Status = EnrollmentStatus.Active;
        ActivatedAt = DateTime.UtcNow;
    }

    public void Complete()
    {
        if (Status != EnrollmentStatus.Active)
            throw new GroupsDomainException("Only active enrollments can be completed.");
        
        Status = EnrollmentStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        AddDomainEvent(new EnrollmentCompleted(Id));
    }
    
    // Jedina tacka kroz koju se menja napredak. Vraca true ako je lekcija NOVO
    // zabelezena, false ako je vec bila (event moze da stigne vise puta - isporuka
    // iz RabbitMQ-a je "bar jednom", pa mora biti idempotentno). Kad broj zavrsenih
    // lekcija dostigne totalLessons (poslednja lekcija je zavrsni test), upis se
    // sam zavrsava. Frozen/Cancelled/Searching upis NE napreduje - baca izuzetak,
    // a handler to proverava unapred pa ga samo loguje i ignorise.
    public bool RecordLessonCompleted(Guid lessonId, int totalLessons)
    {
        if (totalLessons <= 0)
            throw new GroupsDomainException("Total lessons must be positive.");

        if (Status != EnrollmentStatus.Active)
            throw new GroupsDomainException($"Cannot record progress for enrollment in status {Status}.");

        if (_completedLessons.Any(l => l.LessonId == lessonId))
            return false;

        _completedLessons.Add(new CompletedLesson(lessonId, DateTime.UtcNow));

        if (_completedLessons.Count >= totalLessons)
            Complete();

        return true;
    }
    
    // Napomena: ovde se NE racuna penal - to je Payments-ova odgovornost (faza 2),
    // konfigurabilno po danu/statusu. Domain samo belezi da je otkazano i kada.
    public void Cancel()
    {
        if (Status is EnrollmentStatus.Cancelled or EnrollmentStatus.Completed)
            throw new GroupsDomainException($"Cannot cancel enrollment in status {Status}.");

        Status = EnrollmentStatus.Cancelled;
        CancelledAt = DateTime.UtcNow;
        AddDomainEvent(new EnrollmentCancelled(Id));
    }
    
    public void Freeze(Guid approvedByUserId)
    {
        if (approvedByUserId == Guid.Empty)
            throw new GroupsDomainException("Freeze must be approved by someone (admin or professor).");

        if (Status != EnrollmentStatus.Active)
            throw new GroupsDomainException("Only active enrollments can be frozen.");

        Status = EnrollmentStatus.Frozen;
        AddDomainEvent(new EnrollmentFrozen(Id, approvedByUserId));
    }
    
    public void Unfreeze()
    {
        if (Status != EnrollmentStatus.Frozen)
            throw new GroupsDomainException("Only frozen enrollments can be unfrozen.");

        Status = EnrollmentStatus.Active;
    }
    
    // Pristup kursu: svi tipovi (uklj. Individual) tek kad njihova Group predje u
    // Active, tj. profesor potvrdi termin i grupa se pokrene (Enrollment.Status
    // postaje Active tad). Zamrznut/zavrsen/otkazan upis nema pristup.
    public bool GrantsAccess() => Status == EnrollmentStatus.Active;
    
    private static void ValidateCommon(Guid userId, Guid courseId)
    {
        if (userId == Guid.Empty)
            throw new GroupsDomainException("User ID cannot be empty.");

        if (courseId == Guid.Empty)
            throw new GroupsDomainException("Course ID cannot be empty.");
    }
}