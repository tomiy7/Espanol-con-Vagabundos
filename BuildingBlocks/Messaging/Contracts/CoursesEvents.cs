namespace Messaging.Contracts;

// Objavljuje Courses kad ucenik zavrsi sve lekcije i zavrsni test za upis (enrollment).
// Groups ga slusa i upis oznacava kao zavrsen (vidi CourseCompletedHandler u Groups).
public sealed record CourseCompleted(Guid EnrollmentId, Guid UserId, Guid CourseId) : IntegrationEvent;
