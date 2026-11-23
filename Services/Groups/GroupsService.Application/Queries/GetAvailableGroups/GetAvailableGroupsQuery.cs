using GroupsService.Domain.Enums;

namespace GroupsService.Application.Queries.GetAvailableGroups;

public record GetAvailableGroupsQuery(Guid CourseId);

public record SlotDto(int SlotIndex, DayOfWeek? DayOfWeek, TimeOnly? Time, Guid? ProfessorId);

public record GroupSummaryDto(
    Guid Id,
    Guid CourseId,
    GroupType Type,
    GroupStatus Status,
    int MemberCount,
    int MinSize,
    int MaxSize,
    DateTime? FormationWindowEndsAt,
    List<SlotDto> Slots);