namespace GroupsService.Application.Commands.AssignProfessorToSlot;

public record AssignProfessorToSlotCommand(
    Guid GroupId, int SlotIndex, DayOfWeek DayOfWeek, TimeOnly Time, Guid ProfessorId, bool AllowReassign = false);