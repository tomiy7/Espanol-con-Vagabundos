using System.Text;
using System.Text.Json;
using Messaging;
using RabbitMQ.Client;

namespace GroupsService.Integration.Tests.Infrastructure;

// Pristup pravom RabbitMQ-u iz testova: objavi poruku kao drugi servis (npr. Courses),
// ili napravi privremeni red da se uhvati ono sto Groups objavljuje.
public sealed class TestBus : IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly string _exchange;

    private TestBus(IConnection connection, IChannel channel, string exchange)
    {
        _connection = connection;
        _channel = channel;
        _exchange = exchange;
    }

    public static async Task<TestBus> ConnectAsync(string exchange = ApiFactory.TestExchange)
    {
        var factory = new ConnectionFactory { Uri = ApiFactory.RabbitMqUri! };
        var connection = await factory.CreateConnectionAsync();
        var channel = await connection.CreateChannelAsync();
        await channel.ExchangeDeclareAsync(exchange, ExchangeType.Topic, durable: true);
        return new TestBus(connection, channel, exchange);
    }

    public Task DeleteQueueAsync(string queue) => _channel.QueueDeleteAsync(queue, ifUnused: false, ifEmpty: false);

    public Task<uint> MessageCountAsync(string queue) => _channel.MessageCountAsync(queue);

    public Task PublishAsync(IntegrationEvent integrationEvent) =>
        PublishRawAsync(EventEnvelope.TypeOf(integrationEvent.GetType()),
            JsonSerializer.SerializeToUtf8Bytes(EventEnvelope.From(integrationEvent), EventEnvelope.JsonOptions));

    public async Task PublishRawAsync(string routingKey, byte[] body) =>
        await _channel.BasicPublishAsync(_exchange, routingKey, body);

    // Privremeni red (brise se sa konekcijom) vezan za dati tip dogadjaja.
    public async Task<string> ListenAsync(params string[] eventTypes)
    {
        var queue = (await _channel.QueueDeclareAsync(string.Empty, durable: false, exclusive: true, autoDelete: true)).QueueName;
        foreach (var eventType in eventTypes)
            await _channel.QueueBindAsync(queue, _exchange, eventType);
        return queue;
    }

    public async Task<string?> ReceiveAsync(string queue, TimeSpan timeout)
    {
        var until = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < until)
        {
            var result = await _channel.BasicGetAsync(queue, autoAck: true);
            if (result is not null)
                return Encoding.UTF8.GetString(result.Body.Span);
            await Task.Delay(100);
        }
        return null;
    }

    public async ValueTask DisposeAsync()
    {
        await _channel.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
