namespace GroupsService.API.DTOs;

public class CreateIndividualEnrollmentRequest
{
    public Guid CourseId { get; set; }
    public Guid ProfessorId { get; set; }
}

// Ucenik sam bira grupu (termin) u koju ulazi.
public class JoinGroupRequest
{
    public Guid GroupId { get; set; }
}

// Profesor/admin ubacuje ucenika u grupu.
public class AddMemberRequest
{
    public Guid UserId { get; set; }
}

public class SlotRequest
{
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly Time { get; set; }
    public Guid ProfessorId { get; set; }
}

// Profesor/admin otvara grupu sa gotovim rasporedom (tacno 3 termina, npr. pon/sre/pet).
// FormationWindowDays je opciono (default iz konfiguracije).
public class CreateGroupRequest
{
    public Guid CourseId { get; set; }
    public List<SlotRequest> Slots { get; set; } = new();
    public int? FormationWindowDays { get; set; }
}

public class InvitePartnerRequest
{
    public Guid CourseId { get; set; }
    public Guid InviteeUserId { get; set; }
}

// Body je opcioni - "" ili bez body-ja znaci force=false (default). Admin eksplicitno
// salje { "force": true } kad svesno zeli da pokrene ispod MinSize.
public class StartGroupRequest
{
    public bool Force { get; set; }

    // Opciono: od kada se broje nedelje za otvaranje lekcija (UTC). Bez vrednosti = trenutak starta.
    public DateTime? UnlockStartsAt { get; set; }
}

public class AssignProfessorToSlotRequest
{
    public int SlotIndex { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly Time { get; set; }
    public Guid ProfessorId { get; set; }
}
