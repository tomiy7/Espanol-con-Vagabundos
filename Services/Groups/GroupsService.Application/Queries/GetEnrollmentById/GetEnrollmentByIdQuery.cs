using GroupsService.Domain.Enums;

namespace GroupsService.Application.Queries.GetEnrollmentById;

public record GetEnrollmentByIdQuery(Guid EnrollmentId);

public record EnrollmentDetailDto(
    Guid Id,
    Guid UserId,
    Guid CourseId,
    EnrollmentType Type,
    EnrollmentStatus Status,
    Guid? ProfessorId,
    Guid? GroupId,
    DateTime CreatedAt,
    DateTime? ActivatedAt,
    DateTime? CompletedAt);