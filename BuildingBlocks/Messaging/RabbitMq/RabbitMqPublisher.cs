using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Messaging.RabbitMq;

// Objavljuje poruku i ceka potvrdu brokera (publisher confirms): tek kad RabbitMQ
// potvrdi prijem, outbox oznacava poruku kao poslatu. Bez potvrde poruka ostaje u
// outbox-u i salje se ponovo (zato potrosaci moraju da podnose duplikate).
public sealed class RabbitMqPublisher : IMessagePublisher
{
    private readonly RabbitMqConnection _connection;
    private readonly RabbitMqOptions _options;

    public RabbitMqPublisher(RabbitMqConnection connection, IOptions<RabbitMqOptions> options)
    {
        _connection = connection;
        _options = options.Value;
    }

    public async Task PublishAsync(EventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var connection = await _connection.GetAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken);

        await RabbitMqTopology.DeclareExchangeAsync(channel, _options, cancellationToken);

        var properties = new BasicProperties
        {
            Persistent = true,
            MessageId = envelope.EventId.ToString(),
            Type = envelope.EventType,
            ContentType = "application/json",
            Timestamp = new AmqpTimestamp(new DateTimeOffset(envelope.OccurredAt).ToUnixTimeSeconds())
        };

        var body = JsonSerializer.SerializeToUtf8Bytes(envelope, EventEnvelope.JsonOptions);

        // mandatory: poruka bez ijednog vezanog reda se ne vraca kao greska - to je ok,
        // dogadjaj moze da nema slusaoce (npr. Payments jos nije podignut).
        await channel.BasicPublishAsync(
            _options.Exchange, envelope.EventType, mandatory: false, properties, body, cancellationToken);
    }
}
