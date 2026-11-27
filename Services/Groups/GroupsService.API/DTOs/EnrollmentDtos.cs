using GroupsService.Domain.Enums;

namespace GroupsService.API.DTOs;

public class CreateIndividualEnrollmentRequest
{
    public Guid CourseId { get; set; }
    public Guid ProfessorId { get; set; }

    // Opciono: "Full" (podrazumevano) ili "Installments".
    public PaymentPlan? PaymentPlan { get; set; }
}

// Ucenik sam bira grupu (termin) u koju ulazi.
public class JoinGroupRequest
{
    public Guid GroupId { get; set; }
    public PaymentPlan? PaymentPlan { get; set; }
}

// Profesor/admin ubacuje ucenika u grupu.
public class AddMemberRequest
{
    public Guid UserId { get; set; }

    // Izbor ucenika (npr. odgovor iz Google forme); bez vrednosti = odjednom.
    public PaymentPlan? PaymentPlan { get; set; }
}

// Telo je opciono: { "paymentPlan": "Installments" }.
public class AcceptInvitationRequest
{
    public PaymentPlan? PaymentPlan { get; set; }
}

public class ChangePaymentPlanRequest
{
    public PaymentPlan PaymentPlan { get; set; }
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

    // Izbor placanja inicijatora; pozvani bira svoj pri prihvatanju.
    public PaymentPlan? PaymentPlan { get; set; }
}

// Planirani datum i vreme prvog casa. Preporuka je slati sa zonom (npr. "...Z" ili "+01:00");
// bez zone se tumaci kao UTC.
public class SchedulePlannedStartRequest
{
    public DateTime PlannedStartAt { get; set; }
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
