using GroupsService.Domain.Enums;
using GroupsService.Domain.Events;
using GroupsService.Domain.Exceptions;
using GroupsService.Domain.ValueObjects;

namespace GroupsService.Domain.Entities;

// Koordinacioni mehanizam za SVA TRI tipa Enrollment-a, uklj. Individual. Razlika je
// u MinSize/MaxSize i broju slotova: Pair/Group rotiraju kroz 3 profesora nedeljno
// (svaki drzi po jedan termin), Individual ima tacno 1 slot - fiksni profesor, bez
// rotacije, ali i on mora da potvrdi termin (AssignProfessorToSlot) pre nego sto
// Enrollment postane Active - isti mehanizam, samo MinSize=MaxSize=1 i 1 slot.
public class Group : AggregateRoot
{
    // Pair i Group tip imaju po 3 nedeljna termina (npr. pon/sre/pet).
    public const int WeeklySlotCount = 3;

    // Granice protiv besmislenih vrednosti (greska u unosu ili namerna zloupotreba).
    public const int MaxFormationWindowDays = 90;
    public static readonly TimeSpan MaxUnlockOffset = TimeSpan.FromDays(365);
    public static readonly TimeSpan MaxPlannedStartOffset = TimeSpan.FromDays(365);

    public Guid CourseId { get; private set; }
    public GroupType Type { get; private set; }
    public GroupStatus Status { get; private set; }

    // Samo za Group tip - Pair se spaja odmah pri prihvatanju poziva, nema prozora.
    public DateTime? FormationWindowEndsAt { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }

    // Od kog trenutka se broje nedelje za otvaranje lekcija. Po defaultu je to start
    // grupe, a profesor/admin ga pri Start moze postaviti na kasniji datum.
    public DateTime? UnlockStartsAt { get; private set; }

    // Planirani datum i vreme prvog casa (UTC). Postavlja ga profesor/admin pre starta.
    public DateTime? PlannedStartAt { get; private set; }

    // Kada je grupa prvi put imala i dovoljno clanova i datum prvog casa. Postavlja se
    // jednom i ne brise se, i kad grupa kasnije padne ispod minimuma (uplatnice ostaju).
    public DateTime? ConfirmedAt { get; private set; }

    // Konkurentnost: raste pri svakoj promeni clanova. EF ga koristi kao concurrency
    // token, pa dva paralelna upisa na zadnje mesto ne mogu oba da prodju - drugi
    // dobija DbUpdateConcurrencyException (hvata se u RepositoryBase).
    public int Version { get; private set; }

    private readonly List<GroupMember> _members = new();
    public IReadOnlyCollection<GroupMember> Members => _members.AsReadOnly();
    public int MemberCount => _members.Count;

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

    // Individual sada ide kroz isti Group/Slot mehanizam kao Pair/Group - samo sa
    // tacno 1 clanom i 1 slotom, jer profesor (fiksan, bira ga korisnik pri kupovini -
    // vidi Enrollment.CreateIndividual) mora da potvrdi termin pre aktivacije.
    public static Group CreateIndividual(Guid courseId)
    {
        var group = CreateInternal(courseId, GroupType.Individual, slotCount: 1);
        return group;
    }

    public static Group CreatePair(Guid courseId)
    {
        var group = CreateInternal(courseId, GroupType.Pair, slotCount: WeeklySlotCount);
        return group;
    }

    public static Group CreateGroup(Guid courseId, int formationWindowDays)
    {
        if (formationWindowDays <= 0)
            throw new GroupsDomainException("Formation window must be positive.");

        if (formationWindowDays > MaxFormationWindowDays)
            throw new GroupsDomainException($"Formation window cannot be longer than {MaxFormationWindowDays} days.");

        var group = CreateInternal(courseId, GroupType.Group, slotCount: WeeklySlotCount);
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

    public void AddMember(Guid enrollmentId, Guid userId)
    {
        if (Status is not (GroupStatus.Forming or GroupStatus.Ready))
            throw new GroupsDomainException($"Cannot join group in status {Status}.");

        if (_members.Any(m => m.EnrollmentId == enrollmentId))
            throw new GroupsDomainException("Enrollment is already a member of this group.");

        if (_members.Count >= MaxSize)
            throw new GroupsDomainException("Group is full.");

        // Race condition (dva paralelna Join-a) resava Version concurrency token.

        _members.Add(new GroupMember(enrollmentId, userId, DateTime.UtcNow));
        Version++;
        AddDomainEvent(new GroupMemberJoined(Id, enrollmentId));

        if (ConfirmedAt.HasValue)
            AddDomainEvent(new MemberJoinedConfirmedGroup(Id, enrollmentId, userId));

        if (Status == GroupStatus.Forming && _members.Count >= MinSize)
        {
            Status = GroupStatus.Ready;
            AddDomainEvent(new GroupBecameReady(Id));
        }

        ConfirmIfPossible(DateTime.UtcNow);
    }

    // Postavljanje ili pomeranje planiranog datuma prvog casa, dok grupa nije pocela.
    // Isti datum ponovo ne menja nista (ni dogadjaj ni mejl).
    public void SchedulePlannedStart(DateTime at, DateTime now)
    {
        if (Status is not (GroupStatus.Forming or GroupStatus.Ready))
            throw new GroupsDomainException($"First lesson date can only be set before the group starts (status: {Status}).");

        if (at <= now)
            throw new GroupsDomainException("First lesson date must be in the future.");

        if (at > now + MaxPlannedStartOffset)
            throw new GroupsDomainException("First lesson date cannot be more than a year ahead.");

        if (PlannedStartAt == at)
            return;

        // Javno pomeranje samo za potvrdjenu grupu: samo njeni clanovi znaju stari datum
        // (i samo za nju postoje uplatnice). Pre potvrde je promena interna.
        var old = PlannedStartAt;
        PlannedStartAt = at;
        AddDomainEvent(old.HasValue && ConfirmedAt.HasValue
            ? new FirstLessonRescheduled(Id, old.Value, at)
            : new FirstLessonScheduled(Id));

        ConfirmIfPossible(now);
    }

    private void ConfirmIfPossible(DateTime now)
    {
        if (ConfirmedAt.HasValue || Status != GroupStatus.Ready || !PlannedStartAt.HasValue)
            return;

        ConfirmedAt = now;
        AddDomainEvent(new GroupConfirmed(Id));
    }

    // Poziva CancelEnrollmentHandler kad clan otkaze upis - bez ovoga Group._members
    // ostaje "lazno pun" (vidi nalaz u reviewu: Start() bi prosao proveru MinSize sa
    // clanom koji vise ne postoji). Ako je bila Ready a padne ispod MinSize, vracamo
    // na Forming (ponovo se pojavljuje medju grupama u koje moze da se udje) - ali to NE
    // sprecava admin/profesora da ipak odluci da ide dalje sa manje clanova, vidi
    // Start(bool force).
    public void RemoveMember(Guid enrollmentId)
    {
        if (Status == GroupStatus.Completed)
            throw new GroupsDomainException("Cannot remove a member from a completed group.");

        var member = _members.FirstOrDefault(m => m.EnrollmentId == enrollmentId)
            ?? throw new GroupsDomainException("Enrollment is not a member of this group.");

        _members.Remove(member);
        Version++;
        AddDomainEvent(new GroupMemberRemoved(Id, enrollmentId));

        if (Status == GroupStatus.Ready && _members.Count < MinSize)
        {
            Status = GroupStatus.Forming;
            AddDomainEvent(new GroupDroppedBelowMinSize(Id, _members.Count, MinSize));
        }
    }

    // allowReassign=false: slot koji vec ima profesora moze da se promeni samo uz
    // allowReassign=true (admin). Ponovna dodela istih vrednosti je uvek ok.
    public void AssignProfessorToSlot(
        int slotIndex, DayOfWeek dayOfWeek, TimeOnly time, Guid professorId, bool allowReassign = false)
    {
        if (Status == GroupStatus.Completed)
            throw new GroupsDomainException("Cannot assign a professor to a completed group.");

        var slot = _slots.FirstOrDefault(s => s.SlotIndex == slotIndex)
            ?? throw new GroupsDomainException($"Slot {slotIndex} does not exist.");

        var changesExisting = slot.IsAssigned
            && (slot.ProfessorId != professorId || slot.DayOfWeek != dayOfWeek || slot.Time != time);
        if (changesExisting && !allowReassign)
            throw new GroupsDomainException("This slot already has a professor assigned. Only an admin can change it.");

        slot.Assign(dayOfWeek, time, professorId);
        AddDomainEvent(new SlotAssigned(Id, slotIndex, professorId));
    }

    // force=true dozvoljava adminu/profesoru da ipak pokrene grupu i ispod MinSize
    // (npr. neko otkazao poslednji trenutak, ali ostali ne zele da cekaju novu rundu
    // formiranja - "ipak ce biti grupa"). I uz force, mora postojati makar 1 clan -
    // ne postoji "grupa" ili "individualni" cas bez nikoga.
    // unlockStartsAt: od kada se broje nedelje za otvaranje lekcija (default = sada).
    public void Start(bool force = false, DateTime? unlockStartsAt = null, DateTime? now = null)
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

        var startedAt = now ?? DateTime.UtcNow;

        // Otkljucavanje moze da krene ranije (grupa je vec radila) ili kasnije, ali ne
        // godinama daleko od starta.
        if (unlockStartsAt.HasValue
            && (unlockStartsAt.Value < startedAt - MaxUnlockOffset || unlockStartsAt.Value > startedAt + MaxUnlockOffset))
            throw new GroupsDomainException("Lesson unlock date must be within a year of the group start.");

        Status = GroupStatus.Active;
        StartedAt = startedAt;
        UnlockStartsAt = unlockStartsAt ?? startedAt;
        AddDomainEvent(new GroupStarted(Id, CourseId));
    }

    // Rasformiranje grupe koja jos nije pocela (npr. Pair kad partner otkaze, ili
    // Individual kad korisnik otkaze). Clanovi se skidaju, a handler vraca njihove
    // upise u Searching. Rasformirana grupa ne drzi termine profesora.
    public void Dissolve()
    {
        if (Status is not (GroupStatus.Forming or GroupStatus.Ready))
            throw new GroupsDomainException($"Only a forming or ready group can be dissolved (status: {Status}).");

        // Dogadjaj pamti ko je bio u grupi (posle Clear() to vise ne pise nigde).
        var formerMembers = _members.Select(m => new MemberRef(m.EnrollmentId, m.UserId)).ToList();
        _members.Clear();
        Version++;
        Status = GroupStatus.Dissolved;
        AddDomainEvent(new GroupDissolved(Id, formerMembers));
    }

    // Zatvaranje grupe. Poziva se (1) automatski kad svi clanovi koji nisu otkazali
    // zavrse kurs (CourseCompletedHandler), ili (2) rucno od admina/profesora
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

    // Clan je zavrsio kurs. Sadrzaj grupe se ne menja, ali se Version povecava da bi
    // dva istovremena zavrsetka (npr. poslednja dva clana) bila serijalizovana: drugi upis
    // dobija konflikt, ponavlja se, vidi prvog kao zavrsenog i zatvara grupu. Bez ovoga
    // svaki vidi onog drugog kao aktivnog i grupa ostaje otvorena zauvek.
    public void RecordMemberCompleted(Guid enrollmentId)
    {
        if (!HasMember(enrollmentId))
            throw new GroupsDomainException("Enrollment is not a member of this group.");

        Version++;
    }

    public bool HasMember(Guid enrollmentId) => _members.Any(m => m.EnrollmentId == enrollmentId);
}
