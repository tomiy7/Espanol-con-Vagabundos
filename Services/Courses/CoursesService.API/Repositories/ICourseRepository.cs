using CoursesService.API.Entities;

namespace CoursesService.API.Repositories;

public interface ICourseRepository
{
    Task<List<Course>> GetPublishedCoursesAsync(CancellationToken cancellationToken);
    Task<Course?> GetPublishedBySlugWithLessonsAsync(string slug, CancellationToken cancellationToken);
    Task<Course?> GetPublishedByIdWithLessonsAsync(Guid id, CancellationToken cancellationToken);

    // Bez obzira na objavljenost (cene za vec pokrenute grupe).
    Task<Course?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    // Objavljena i vidljiva lekcija, sa sekcijama i zadacima i kursom.
    Task<Lesson?> GetLessonAsync(Guid id, CancellationToken cancellationToken);

    // Objavljene i vidljive lekcije kursa (id + order), za racunanje pozicije lekcije.
    Task<List<(Guid Id, int Order)>> GetLessonOrderAsync(Guid courseId, CancellationToken cancellationToken);
}
