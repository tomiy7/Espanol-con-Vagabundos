using GroupsService.Application.Interfaces;

namespace GroupsService.Application.Queries.CheckAccess;

public class CheckAccessHandler
{
    private readonly IEnrollmentRepository _repository;

    public CheckAccessHandler(IEnrollmentRepository repository)
    {
        _repository = repository;
    }
    
    public async Task<CheckAccessResult> Handle(CheckAccessQuery query)
    {
        // Odgovara na "da li ovaj korisnik ima pristup ovom kursu" - poziva ga Courses
        // servis sa GET /courses/{id}/access (taj endpoint tamo jos uvek ne postoji,
        // trenutno je stub koji uvek vraca false - vidi napomene u KT dokumentu).
        // Odgovor nosi i EnrollmentId upisa koji daje pristup - Courses ga stavlja u
        // LessonCompleted event.
        var enrollments = await _repository.GetByUserIdAsync(query.UserId);

        var granting = enrollments.FirstOrDefault(e => e.CourseId == query.CourseId && e.GrantsAccess());
        return new CheckAccessResult(granting is not null, granting?.Id);
    }
}