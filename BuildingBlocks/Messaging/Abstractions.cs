namespace Messaging;

// Obrada jednog tipa dogadjaja. Implementacija je u Infrastructure sloju servisa i samo
// prevodi dogadjaj u komandu Application sloja. Baceni izuzetak znaci "nije obradjeno":
// poruka se ponavlja, a posle MaxDeliveryAttempts ide u dead-letter red.
public interface IIntegrationEventHandler<in TEvent> where TEvent : IntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken);
}

// Slanje vec serijalizovane poruke (koristi ga outbox dispatcher).
public interface IMessagePublisher
{
    Task PublishAsync(EventEnvelope envelope, CancellationToken cancellationToken = default);
}
