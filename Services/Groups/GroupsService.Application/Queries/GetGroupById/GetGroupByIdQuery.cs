using GroupsService.Application.Queries.GetAvailableGroups;
using GroupsService.Domain.Enums;

namespace GroupsService.Application.Queries.GetGroupById;

public record GetGroupByIdQuery(Guid GroupId);

// Clan grupe za profesora/admina: upis, korisnik (ime iz Auth-a, prazno ako nije
// dostupno), status upisa i napredak.
public record GroupMemberDto(
    Guid EnrollmentId,
    Guid UserId,
    string? FullName,
    EnrollmentStatus Status,
    int CompletedLessons);

public record GroupDetailDto(
    Guid Id,
    Guid CourseId,
    GroupType Type,
    GroupStatus Status,
    int MinSize,
    int MaxSize,
    DateTime? FormationWindowEndsAt,
    DateTime? StartedAt,
    List<SlotDto> Slots,
    List<GroupMemberDto> Members);
