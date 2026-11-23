namespace GroupsService.Application.Commands.CreateGroup;

public record SlotInput(DayOfWeek DayOfWeek, TimeOnly Time, Guid ProfessorId);

// IsAdmin odredjuje kontroler iz JWT role. Profesor sme da otvori grupu samo sa
// sobom kao profesorom svih termina, admin moze da postavi bilo kog profesora.
// FormationWindowDays je opciono - ako nije dato, uzima se EnrollmentOptions default.
public record CreateGroupCommand(
    Guid RequestingUserId,
    bool IsAdmin,
    Guid CourseId,
    List<SlotInput> Slots,
    int? FormationWindowDays = null);

public record CreateGroupResult(Guid GroupId);
