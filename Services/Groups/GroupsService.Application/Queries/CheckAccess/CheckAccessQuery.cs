namespace GroupsService.Application.Queries.CheckAccess;

public record CheckAccessQuery(Guid UserId, Guid CourseId);

// EnrollmentId je upis koji daje pristup (null ako pristupa nema). Courses ga uzima
// odavde i stavlja u LessonCompleted event, pa Groups ne mora da trazi upis po
// (korisnik, kurs). Zavrsen kurs se ne pohadja ponovo, ali posle otkazivanja korisnik
// moze da se upise ponovo - tada je to NOV upis sa napretkom od nule.
public record CheckAccessResult(bool HasAccess, Guid? EnrollmentId = null);