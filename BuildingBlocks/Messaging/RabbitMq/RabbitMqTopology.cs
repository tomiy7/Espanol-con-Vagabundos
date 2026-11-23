using RabbitMQ.Client;

namespace Messaging.RabbitMq;

// Deklaracije su idempotentne (isti parametri = no-op), pa ih i publisher i potrosac
// rade na startu, bez obzira ko se prvi podigne.
public static class RabbitMqTopology
{
    public static string DeadLetterExchange(RabbitMqOptions options) => $"{options.Exchange}.dead";

    public static string DeadLetterQueue(RabbitMqOptions options) => $"{options.QueueName}.dead";

    public static Task DeclareExchangeAsync(IChannel channel, RabbitMqOptions options, CancellationToken cancellationToken) =>
        channel.ExchangeDeclareAsync(options.Exchange, ExchangeType.Topic, durable: true, cancellationToken: cancellationToken);

    // Red servisa vezan za tipove dogadjaja koje obradjuje, plus dead-letter red u koji
    // stizu poruke odbijene posle svih pokusaja (za rucni pregled i ponovno slanje).
    public static async Task DeclareQueueAsync(
        IChannel channel, RabbitMqOptions options, IEnumerable<string> eventTypes, CancellationToken cancellationToken)
    {
        await DeclareExchangeAsync(channel, options, cancellationToken);

        var deadExchange = DeadLetterExchange(options);
        var deadQueue = DeadLetterQueue(options);
        await channel.ExchangeDeclareAsync(deadExchange, ExchangeType.Direct, durable: true, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(deadQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(deadQueue, deadExchange, options.QueueName, cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(options.QueueName, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = deadExchange,
                ["x-dead-letter-routing-key"] = options.QueueName
            },
            cancellationToken: cancellationToken);

        foreach (var eventType in eventTypes)
            await channel.QueueBindAsync(options.QueueName, options.Exchange, eventType, cancellationToken: cancellationToken);
    }
}
