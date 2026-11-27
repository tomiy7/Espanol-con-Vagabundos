using GroupsService.Application.Interfaces;

namespace GroupsService.Application.Queries.GetMyEnrollments;

// Jedini nacin do sada da student vidi SOPSTVENE upise bio je GetEnrollmentById
// (jedan po jedan, mora da zna Id) - ovo je prvi endpoint koji vraca kompletnu
// listu, uklj. Completed/Cancelled, bas za "zavrseni kursevi" prikaz.
public class GetMyEnrollmentsHandler
{
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly ICourseDirectory _courseDirectory;

    public GetMyEnrollmentsHandler(IEnrollmentRepository enrollmentRepository, ICourseDirectory courseDirectory)
    {
        _enrollmentRepository = enrollmentRepository;
        _courseDirectory = courseDirectory;
    }

    public async Task<List<MyEnrollmentDto>> Handle(GetMyEnrollmentsQuery query)
    {
        var enrollments = await _enrollmentRepository.GetByUserIdAsync(query.UserId);

        var result = new List<MyEnrollmentDto>(enrollments.Count);
        foreach (var enrollment in enrollments.OrderByDescending(e => e.CreatedAt))
        {
            var courseName = await _courseDirectory.GetCourseNameAsync(enrollment.CourseId);

            result.Add(new MyEnrollmentDto(
                Id: enrollment.Id,
                CourseId: enrollment.CourseId,
                CourseName: courseName,
                Type: enrollment.Type,
                Status: enrollment.Status,
                PaymentPlan: enrollment.PaymentPlan,
                GroupId: enrollment.GroupId,
                CreatedAt: enrollment.CreatedAt,
                ActivatedAt: enrollment.ActivatedAt,
                CancelledAt: enrollment.CancelledAt,
                CompletedAt: enrollment.CompletedAt));
        }

        return result;
    }
}