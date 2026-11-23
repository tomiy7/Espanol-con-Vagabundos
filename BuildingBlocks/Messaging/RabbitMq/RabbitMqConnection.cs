using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Messaging.RabbitMq;

// Jedna dugovecna konekcija po procesu (preporuka RabbitMQ-a); kanali su kratkovecni.
// Klijent sam obnavlja prekinutu konekciju (AutomaticRecoveryEnabled) zajedno sa njenim
// kanalima i potrosacima. Zato se postojeca konekcija NIKAD ne odbacuje dok je u oporavku:
// odbacivanje bi ugasilo i kanal potrosaca, pa servis ne bi vise primao poruke.
// Nova konekcija se pravi samo ako jos nijedna nije uspostavljena (broker nije radio na startu).
public sealed class RabbitMqConnection : IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection? _connection;

    public RabbitMqConnection(IOptions<RabbitMqOptions> options)
    {
        _options = options.Value;
    }

    // Da li je broker trenutno dostupan (bez pokusaja povezivanja).
    public bool IsOpen => _connection is { IsOpen: true };

    public async Task<IConnection> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is not null)
            return _connection;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_connection is not null)
                return _connection;

            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                VirtualHost = _options.VirtualHost,
                UserName = _options.UserName,
                Password = _options.Password,
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                ClientProvidedName = string.IsNullOrEmpty(_options.QueueName) ? null : _options.QueueName
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken);
            return _connection;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
        _lock.Dispose();
    }
}
