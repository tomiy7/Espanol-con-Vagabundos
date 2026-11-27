using GroupsService.Application.Interfaces;
using GroupsService.Application.Options;
using GroupsService.Domain.Enums;
using Microsoft.Extensions.Options;

namespace GroupsService.Application.Queries.CheckAccess;

public class CheckAccessHandler
{
    private readonly IEnrollmentRepository _repository;
    private readonly IGroupRepository _groupRepository;
    private readonly EnrollmentOptions _options;

    public CheckAccessHandler(
        IEnrollmentRepository repository,
        IGroupRepository groupRepository,
        IOptions<EnrollmentOptions> options)
    {
        _repository = repository;
        _groupRepository = groupRepository;
        _options = options.Value;
    }
    
    public async Task<CheckAccessResult> Handle(CheckAccessQuery query)
    {
        // Odgovara na "da li ovaj korisnik ima pristup ovom kursu" - poziva ga Courses
        // servis sa GET /courses/{id}/access (taj endpoint tamo jos uvek ne postoji,
        // trenutno je stub koji uvek vraca false - vidi napomene u KT dokumentu).
        // Odgovor nosi i EnrollmentId upisa koji daje pristup - Courses po njemu vodi
        // napredak i stavlja ga u CourseCompleted event.
        var enrollments = await _repository.GetByUserIdAsync(query.UserId);

        // Aktivan upis ima prednost nad zavrsenim (npr. ucenik ponovo slusa isti kurs).
        var now = DateTime.UtcNow;
        var granting = enrollments
            .Where(e => e.CourseId == query.CourseId && e.GrantsAccess(now, _options.CompletedAccessDays))
            .OrderBy(e => e.Status == EnrollmentStatus.Active ? 0 : 1)
            .FirstOrDefault();
        if (granting is null)
            return new CheckAccessResult(false);

        // Zavrsen kurs: otkljucano je ono sto je bilo otkljucano u trenutku zavrsetka
        // (zavrsni test se otvara tek posle svih lekcija, pa je to ceo kurs).
        var unlocked = 0;
        if (granting.GroupId.HasValue)
        {
            var group = await _groupRepository.GetByIdAsync(granting.GroupId.Value);
            if (group?.UnlockStartsAt is { } unlockStartsAt)
                unlocked = granting.UnlockedLessons(unlockStartsAt, _options.LessonsPerWeek, granting.CompletedAt ?? now);
        }

        return new CheckAccessResult(true, granting.Id, unlocked, granting.AccessUntil(_options.CompletedAccessDays));
    }
}