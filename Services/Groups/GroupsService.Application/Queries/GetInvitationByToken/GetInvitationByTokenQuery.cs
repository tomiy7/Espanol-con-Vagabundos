using GroupsService.Domain.Enums;

namespace GroupsService.Application.Queries.GetInvitationByToken;

public record GetInvitationByTokenQuery(string Token);

public record InvitationDetailDto(
    Guid Id,
    InvitationStatus Status,
    bool IsExpired,
    Guid CourseId,
    string CourseName);