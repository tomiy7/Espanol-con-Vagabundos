using GroupsService.Application.Interfaces;

namespace GroupsService.Application.Queries.GetEnrollmentById;

public class GetEnrollmentByIdHandler
{
    private readonly IEnrollmentRepository _repository;

    public GetEnrollmentByIdHandler(IEnrollmentRepository repository)
    {
        _repository = repository;
    }

    public async Task<EnrollmentDetailDto?> Handle(GetEnrollmentByIdQuery query)
    {
        var enrollment = await _repository.GetByIdAsync(query.EnrollmentId);
        if (enrollment == null) return null;

        return new EnrollmentDetailDto(
            Id: enrollment.Id,
            UserId: enrollment.UserId,
            CourseId: enrollment.CourseId,
            Type: enrollment.Type,
            Status: enrollment.Status,
            ProfessorId: enrollment.ProfessorId,
            GroupId: enrollment.GroupId,
            CreatedAt: enrollment.CreatedAt,
            ActivatedAt: enrollment.ActivatedAt,
            CompletedAt: enrollment.CompletedAt,
            CompletedLessons: enrollment.CompletedLessonCount);
    }
}