using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Messaging.RabbitMq;

// Slusa red servisa i svaku poruku predaje registrovanom handler-u (u novom DI scope-u,
// kao da je HTTP zahtev). Pravila isporuke:
//  - uspeh -> ack;
//  - izuzetak -> ponovni pokusaj sa pauzom (svaki put nov scope, pa i svez DbContext),
//    sto resava prolazne greske poput izgubljene trke (ConcurrencyConflictException);
//  - posle MaxDeliveryAttempts, ili za poruku koja ne moze da se procita -> nack bez
//    vracanja u red, pa RabbitMQ poruku prebacuje u dead-letter red "{QueueName}.dead".
// Poruke se obradjuju jedna po jedna (podrazumevano u RabbitMQ.Client 7).
public sealed class RabbitMqConsumerService : BackgroundService
{
    private readonly RabbitMqConnection _connection;
    private readonly SubscriptionRegistry _subscriptions;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqConsumerService> _logger;
    private IChannel? _channel;

    public RabbitMqConsumerService(
        RabbitMqConnection connection,
        SubscriptionRegistry subscriptions,
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqConsumerService> logger)
    {
        _connection = connection;
        _subscriptions = subscriptions;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_subscriptions.All.Count == 0)
            return;

        // Broker moze da se podigne posle servisa - pokusava se dok ne uspe.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var connection = await _connection.GetAsync(stoppingToken);
                _channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
                await RabbitMqTopology.DeclareQueueAsync(
                    _channel, _options, _subscriptions.All.Keys, stoppingToken);
                await _channel.BasicQosAsync(0, _options.PrefetchCount, global: false, stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(_channel);
                consumer.ReceivedAsync += (_, delivery) => OnReceivedAsync(delivery, stoppingToken);
                await _channel.BasicConsumeAsync(_options.QueueName, autoAck: false, consumer, stoppingToken);

                _logger.LogInformation("Consuming queue {Queue} for {EventTypes}.",
                    _options.QueueName, string.Join(", ", _subscriptions.All.Keys));
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Cannot connect to RabbitMQ at {Host}:{Port}, retrying in 5s.",
                    _options.HostName, _options.Port);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ContinueWith(_ => { });
            }
        }
    }

    private async Task OnReceivedAsync(BasicDeliverEventArgs delivery, CancellationToken stoppingToken)
    {
        var channel = _channel!;

        EventEnvelope? envelope;
        Subscription? subscription;
        IntegrationEvent? integrationEvent;
        try
        {
            envelope = JsonSerializer.Deserialize<EventEnvelope>(delivery.Body.Span, EventEnvelope.JsonOptions);
            subscription = envelope is null ? null : _subscriptions.Find(envelope.EventType);
            integrationEvent = envelope is null || subscription is null
                ? null
                : (IntegrationEvent?)envelope.Data.Deserialize(subscription.EventClrType, EventEnvelope.JsonOptions);
        }
        catch (JsonException e)
        {
            _logger.LogError(e, "Unreadable message on {Queue}, moving it to the dead-letter queue.", _options.QueueName);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, stoppingToken);
            return;
        }

        if (envelope is null || subscription is null || integrationEvent is null)
        {
            _logger.LogError("Message of unknown type {EventType} on {Queue}, moving it to the dead-letter queue.",
                envelope?.EventType, _options.QueueName);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, stoppingToken);
            return;
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await subscription.Handle(scope.ServiceProvider, integrationEvent, stoppingToken);
                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, stoppingToken);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Gasenje: poruka se vraca u red i obradjuje posle restarta.
                await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: true, CancellationToken.None);
                return;
            }
            catch (Exception e) when (attempt < _options.MaxDeliveryAttempts)
            {
                _logger.LogWarning(e, "Handling {EventType} {EventId} failed (attempt {Attempt}/{Max}), retrying.",
                    envelope.EventType, envelope.EventId, attempt, _options.MaxDeliveryAttempts);
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), stoppingToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Handling {EventType} {EventId} failed after {Max} attempts, moving it to the dead-letter queue.",
                    envelope.EventType, envelope.EventId, _options.MaxDeliveryAttempts);
                await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, stoppingToken);
                return;
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        if (_channel is null)
            return;

        // Kanal moze vec biti zatvoren (broker pao, konekcija ugasena pre nas) - gasenje
        // servisa zbog toga ne sme da pukne.
        try
        {
            await _channel.DisposeAsync();
        }
        catch (Exception e)
        {
            _logger.LogDebug(e, "RabbitMQ channel was already closed on shutdown.");
        }
    }
}
