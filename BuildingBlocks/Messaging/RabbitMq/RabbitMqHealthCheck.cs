using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Messaging.RabbitMq;

// /health prijavljuje i da li je broker dostupan (bez njega se dogadjaji gomilaju u outbox-u).
public sealed class RabbitMqHealthCheck : IHealthCheck
{
    private readonly RabbitMqConnection _connection;

    public RabbitMqHealthCheck(RabbitMqConnection connection)
    {
        _connection = connection;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            // Konekcija u oporavku se ne dira (vidi RabbitMqConnection) - samo se prijavljuje stanje.
            await _connection.GetAsync(timeout.Token);
            return _connection.IsOpen
                ? HealthCheckResult.Healthy()
                : new HealthCheckResult(context.Registration.FailureStatus, "RabbitMQ connection is closed.");
        }
        catch (Exception e)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "RabbitMQ is not reachable.", e);
        }
    }
}
