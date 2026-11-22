namespace GroupsService.Application.Interfaces;

// Courses servis nema GET /courses/{id} (samo GET /courses i GET /courses/{slug}),
// pa se naziv kursa razresava fetch-ovanjem cele liste i filtriranjem po Id-ju.
// Nije idealno za ucestale pozive, ali Groups retko treba ovo (samo za mejlove),
// pa je prost pristup OK za sada - keširanje je laka buduca optimizacija.
public interface ICourseDirectory
{
    Task<string?> GetCourseNameAsync(Guid courseId, CancellationToken ct = default);
}
