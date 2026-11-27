using GroupsService.Domain.Enums;

namespace GroupsService.Application.Commands.CreateIndividualEnrollment;

public record CreateIndividualEnrollmentCommand(
    Guid UserId, Guid CourseId, Guid ProfessorId, PaymentPlan? PaymentPlan = null);

// GroupId se vraca odmah jer ga admin/profesor treba za AssignProfessorToSlot
// (potvrda termina) - vidi CreateIndividualEnrollmentHandler.
public record CreateIndividualEnrollmentResult(Guid EnrollmentId, Guid GroupId);
