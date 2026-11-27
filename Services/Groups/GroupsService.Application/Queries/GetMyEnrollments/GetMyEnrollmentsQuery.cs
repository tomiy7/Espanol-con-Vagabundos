using GroupsService.Domain.Enums;

namespace GroupsService.Application.Queries.GetMyEnrollments;

public record GetMyEnrollmentsQuery(Guid UserId);

// Isti oblik kao EnrollmentDetailDto (GetEnrollmentById), samo za listu + ime kursa -
// frontend ovo koristi da prikaze "Aktivni kursevi" / "Zavrseni kursevi" / "Otkazani"
// sekcije, grupisanjem po Status na svojoj strani (Searching/Ready/Active idu u
// "aktivno", Completed u "zavrseno", Cancelled u "otkazano").
// Napredak po lekcijama vodi Courses (po upisu), pa ga frontend uzima odatle.
public record MyEnrollmentDto(
    Guid Id,
    Guid CourseId,
    string? CourseName, // null kad Courses nije dostupan - front prikazuje svoj tekst
    EnrollmentType Type,
    EnrollmentStatus Status,
    PaymentPlan PaymentPlan,
    Guid? GroupId,
    DateTime CreatedAt,
    DateTime? ActivatedAt,
    DateTime? CancelledAt,
    DateTime? CompletedAt);