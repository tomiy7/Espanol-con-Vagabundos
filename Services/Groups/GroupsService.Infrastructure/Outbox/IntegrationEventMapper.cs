using GroupsService.Domain;
using GroupsService.Domain.Entities;
using Messaging;
using Contracts = Messaging.Contracts;
using DomainEvents = GroupsService.Domain.Events;

namespace GroupsService.Infrastructure.Outbox;

// Koji domenski dogadjaji postaju javni (integracioni) i sa kojim podacima. Podaci se
// uzimaju iz agregata u trenutku snimanja, pa poruka opisuje stanje koje je upisano.
// Ostali domenski dogadjaji (clan usao, termin dodeljen...) su interni i ne izlaze.
public static class IntegrationEventMapper
{
    public static IntegrationEvent? Map(AggregateRoot aggregate, object domainEvent) => (aggregate, domainEvent) switch
    {
        (Group g, DomainEvents.GroupBecameReady) => new Contracts.GroupBecameReady(
            g.Id, g.CourseId, g.Type.ToString(), g.Members.Select(m => m.EnrollmentId).ToList()),

        (Group g, DomainEvents.GroupStarted) => new Contracts.GroupStarted(
            g.Id, g.CourseId, g.StartedAt!.Value, g.UnlockStartsAt!.Value),

        (Group g, DomainEvents.GroupDissolved) => new Contracts.GroupDissolved(g.Id, g.CourseId),

        (Group g, DomainEvents.GroupCompleted) => new Contracts.GroupCompleted(g.Id, g.CourseId),

        (Enrollment e, DomainEvents.EnrollmentCancelled) => new Contracts.EnrollmentCancelled(e.Id, e.UserId, e.CourseId),

        (Enrollment e, DomainEvents.EnrollmentCompleted) => new Contracts.EnrollmentCompleted(e.Id, e.UserId, e.CourseId),

        _ => null
    };
}
