using GroupsService.Application.Commands.CourseCompleted;
using Messaging;
using CourseCompletedEvent = Messaging.Contracts.CourseCompleted;

namespace GroupsService.Infrastructure.Messaging;

// Prevodi CourseCompleted poruku (iz Courses) u komandu Application sloja. Konflikt sa
// drugim clanom iste grupe (ConcurrencyConflictException) se namerno ne hvata: potrosac
// tada ponavlja poruku u novom scope-u, sa svezim podacima.
public sealed class CourseCompletedEventHandler : IIntegrationEventHandler<CourseCompletedEvent>
{
    private readonly CourseCompletedHandler _handler;

    public CourseCompletedEventHandler(CourseCompletedHandler handler)
    {
        _handler = handler;
    }

    public Task HandleAsync(CourseCompletedEvent integrationEvent, CancellationToken cancellationToken) =>
        _handler.Handle(new CourseCompletedCommand(
            integrationEvent.EnrollmentId, integrationEvent.UserId, integrationEvent.CourseId));
}
