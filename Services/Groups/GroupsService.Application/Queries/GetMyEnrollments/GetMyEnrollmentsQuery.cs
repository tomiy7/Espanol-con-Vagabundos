using GroupsService.Domain.Enums;

namespace GroupsService.Application.Queries.GetMyEnrollments;

public record GetMyEnrollmentsQuery(Guid UserId);

// Isti oblik kao EnrollmentDetailDto (GetEnrollmentById), samo za listu + ime kursa -
// frontend ovo koristi da prikaze "Aktivni kursevi" / "Zavrseni kursevi" / "Otkazani"
// sekcije, grupisanjem po Status na svojoj strani (Searching/Ready/Active idu u
// "aktivno", Completed u "zavrseno", Cancelled u "otkazano").
// Napredak: frontend ima listu lekcija iz Courses, pa iz CompletedLessonIds zna
// koja je gotova, koja je sledeca, i racuna procenat (CompletedLessons / broj
// lekcija) - Groups ne zna ukupan broj lekcija do prvog LessonCompleted event-a.
public record MyEnrollmentDto(
    Guid Id,
    Guid CourseId,
    string CourseName,
    EnrollmentType Type,
    EnrollmentStatus Status,
    Guid? GroupId,
    DateTime CreatedAt,
    DateTime? ActivatedAt,
    DateTime? CancelledAt,
    DateTime? CompletedAt,
    int CompletedLessons,
    List<Guid> CompletedLessonIds);