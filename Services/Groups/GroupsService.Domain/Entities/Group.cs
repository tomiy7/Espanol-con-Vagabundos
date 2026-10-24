using GroupsService.Domain.Enums;
using GroupsService.Domain.Events;
using GroupsService.Domain.Exceptions;
using GroupsService.Domain.ValueObjects;

namespace GroupsService.Domain.Entities;

public class Group : AggregateRoot
{
    public Guid CourseId { get; private set; }
    public GroupType Type { get; private set; }
    public GroupStatus Status { get; private set; }
    
    // Samo za Group tip - Pair se spaja odmah pri prihvatanju poziva, nema prozora.
    public DateTime? FormationWindowEndsAt { get; private set; }
    
    public DateTime CreatedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }

    private readonly List<GroupMember> _members = new();
    public IReadOnlyCollection<GroupMember> Members => _members.AsReadOnly();
    public int MembersCount => _members.Count;
    
    private readonly List<Slot> _slots = new();
    public IReadOnlyCollection<Slot> Slots => _slots.AsReadOnly();
    
    public int MinSize => Type switch
    {
        GroupType.Individual => 1,
        GroupType.Pair => 2,
        _ => 3
    };
    
    public int MaxSize => Type switch
    {
        GroupType.Individual => 1,
        GroupType.Pair => 2,
        _ => 6
    };
    
    private Group() { }

    public static Group CreateIndividual(Guid courseId)
    {
        var group = CreateInternal(courseId, GroupType.Individual, slotCount: 1);
        return group;
    }

    public static Group CreatePair(Guid courseId)
    {
        var group = CreateInternal(courseId, GroupType.Pair, slotCount: 3);
        return group;
    }

    public static Group CreateGroup(Guid courseId, int formationWindowDays)
    {
        if (formationWindowDays <= 0)
            throw new GroupsDomainException("Formation window must be positive.");

        var group = CreateInternal(courseId, GroupType.Group, slotCount: 3);
        group.FormationWindowEndsAt = DateTime.UtcNow.AddDays(formationWindowDays);
        return group;
    }
    
    private static Group CreateInternal(Guid courseId, GroupType type, int slotCount)
    {
        if (courseId == Guid.Empty)
            throw new GroupsDomainException("Course ID cannot be empty.");

        var group = new Group
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            Type = type,
            Status = GroupStatus.Forming,
            CreatedAt = DateTime.UtcNow
        };

        for (var i = 0; i < slotCount; i++)
            group._slots.Add(Slot.CreateEmpty(i));

        return group;
    }
    
    public bool FormationWindowExpired =>
        FormationWindowEndsAt.HasValue && DateTime.UtcNow > FormationWindowEndsAt.Value;

    public void AddMember(Guid enrollmentId)
    {
        if (Status is not (GroupStatus.Forming or GroupStatus.Ready))
            throw new GroupsDomainException($"Cannot join group in status {Status}.");

        if (_members.Any(m => m.EnrollmentId == enrollmentId))
            throw new GroupsDomainException("Enrollment is already a member of this group.");

        if (_members.Count >= MaxSize)
            throw new GroupsDomainException("Group is full.");

        // TODO: race condition - dva paralelna Join-a mogu proci kroz proveru pre
        // nego sto se prvi sacuva. Prihvatljivo za MVP (mali obim, admin nadgleda);
        // ozbiljno resenje je optimisticka konkurentnost (RowVersion) ili DB constraint.
        
        _members.Add(new GroupMember(enrollmentId, DateTime.UtcNow));
        
        if (Status == GroupStatus.Forming && _members.Count >= MinSize)
        {
            Status = GroupStatus.Ready;
            AddDomainEvent(new GroupBecameReady(Id));
        }
    }
    
    // Poziva CancelEnrollmentHandler kad clan otkaze upis - bez ovoga Group._members
    // ostaje "lazno pun" (vidi nalaz u reviewu: Start() bi prosao proveru MinSize sa
    // clanom koji vise ne postoji). Ako je bila Ready a padne ispod MinSize, vracamo
    // na Forming (ponovo "otvorena" za FindOpenGroupForCourseAsync) - ali to NE
    // sprecava admin/profesora da ipak odluci da ide dalje sa manje clanova, vidi
    // Start(bool force).
    public void RemoveMember(Guid enrollmentId)
    {
        if (Status == GroupStatus.Completed)
            throw new GroupsDomainException("Cannot remove a member from a completed group.");

        var member = _members.FirstOrDefault(m => m.EnrollmentId == enrollmentId)
                     ?? throw new GroupsDomainException("Enrollment is not a member of this group.");

        _members.Remove(member);
        AddDomainEvent(new GroupMemberRemoved(Id, enrollmentId));

        if (Status == GroupStatus.Ready && _members.Count < MinSize)
        {
            Status = GroupStatus.Forming;
            AddDomainEvent(new GroupDroppedBelowMinSize(Id, _members.Count, MinSize));
        }
    }
    
    public void AssignProfessorToSlot(int slotIndex, DayOfWeek dayOfWeek, TimeOnly time, Guid professorId)
    {
        if (Status == GroupStatus.Completed)
            throw new GroupsDomainException("Cannot assign a professor to a completed group.");

        var slot = _slots.FirstOrDefault(s => s.SlotIndex == slotIndex)
                   ?? throw new GroupsDomainException($"Slot {slotIndex} does not exist.");

        slot.Assign(dayOfWeek, time, professorId);
        AddDomainEvent(new SlotAssigned(Id, slotIndex, professorId));
    }
    
    // force=true dozvoljava adminu/profesoru da ipak pokrene grupu i ispod MinSize
    // (npr. neko otkazao poslednji trenutak, ali ostali ne zele da cekaju novu rundu
    // formiranja - "ipak ce biti grupa"). I uz force, mora postojati makar 1 clan -
    // ne postoji "grupa" ili "individualni" cas bez nikoga.
    public void Start(bool force = false)
    {
        if (Status == GroupStatus.Active)
            throw new GroupsDomainException("Group is already active.");

        if (Status == GroupStatus.Completed)
            throw new GroupsDomainException("Cannot start a completed group.");

        if (_members.Count == 0)
            throw new GroupsDomainException("Cannot start a group with no members.");

        if (!force && _members.Count < MinSize)
            throw new GroupsDomainException(
                $"Group needs at least {MinSize} members to start (currently {_members.Count}). " +
                "Pass force=true to start anyway with fewer members.");

        if (_slots.Any(s => !s.IsAssigned))
            throw new GroupsDomainException(
                $"All {_slots.Count} weekly slot(s) must have a professor assigned before starting.");

        Status = GroupStatus.Active;
        StartedAt = DateTime.UtcNow;
        AddDomainEvent(new GroupStarted(Id, CourseId));
    }
    
    // Zatvaranje grupe. Poziva se (1) automatski kad svi clanovi koji nisu otkazali
    // zavrse kurs (RecordLessonCompletedHandler), ili (2) rucno od admina/profesora
    // (CompleteGroupHandler). Samo Active grupa moze da se zavrsi - Forming/Ready
    // grupa jos nije ni pocela.
    public void Complete()
    {
        if (Status == GroupStatus.Completed)
            throw new GroupsDomainException("Group is already completed.");

        if (Status != GroupStatus.Active)
            throw new GroupsDomainException($"Only an active group can be completed (status: {Status}).");

        Status = GroupStatus.Completed;
        AddDomainEvent(new GroupCompleted(Id));
    }
    
    public bool HasMember(Guid enrollmentId) => _members.Any(m => m.EnrollmentId == enrollmentId);
}