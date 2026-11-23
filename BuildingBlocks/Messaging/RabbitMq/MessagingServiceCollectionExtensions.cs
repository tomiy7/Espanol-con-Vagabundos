using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Messaging.RabbitMq;

public static class MessagingServiceCollectionExtensions
{
    // Konekcija, publisher, potrosac i health check. Handler-i se dodaju preko AddHandler.
    public static MessagingBuilder AddRabbitMqMessaging(this IServiceCollection services, IConfiguration section)
    {
        services.Configure<RabbitMqOptions>(section);
        services.AddSingleton<RabbitMqConnection>();
        services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();

        var registry = new SubscriptionRegistry();
        services.AddSingleton(registry);
        services.AddHostedService<RabbitMqConsumerService>();

        services.AddHealthChecks().AddCheck<RabbitMqHealthCheck>("rabbitmq", HealthStatus.Unhealthy);

        return new MessagingBuilder(services, registry);
    }
}

public sealed class MessagingBuilder
{
    private readonly IServiceCollection _services;
    private readonly SubscriptionRegistry _registry;

    internal MessagingBuilder(IServiceCollection services, SubscriptionRegistry registry)
    {
        _services = services;
        _registry = registry;
    }

    public MessagingBuilder AddHandler<TEvent, THandler>()
        where TEvent : IntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
    {
        _services.AddScoped<IIntegrationEventHandler<TEvent>, THandler>();
        _registry.Add<TEvent>();
        return this;
    }
}
