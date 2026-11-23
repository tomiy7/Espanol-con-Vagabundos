using Microsoft.Extensions.DependencyInjection;

namespace Messaging.RabbitMq;

public sealed record Subscription(
    Type EventClrType,
    Func<IServiceProvider, IntegrationEvent, CancellationToken, Task> Handle);

// Koji tip dogadjaja obradjuje koji handler. Puni se pri registraciji (AddHandler), a
// potrosac iz njega pravi vezivanja reda (routing key = tip dogadjaja).
public sealed class SubscriptionRegistry
{
    private readonly Dictionary<string, Subscription> _subscriptions = new();

    public IReadOnlyDictionary<string, Subscription> All => _subscriptions;

    public Subscription? Find(string eventType) => _subscriptions.GetValueOrDefault(eventType);

    public void Add<TEvent>() where TEvent : IntegrationEvent =>
        _subscriptions[EventEnvelope.TypeOf<TEvent>()] = new Subscription(
            typeof(TEvent),
            (services, integrationEvent, cancellationToken) => services
                .GetRequiredService<IIntegrationEventHandler<TEvent>>()
                .HandleAsync((TEvent)integrationEvent, cancellationToken));
}
