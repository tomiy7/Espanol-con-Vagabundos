using System.Text.Json;
using GroupsService.Infrastructure.Data;
using Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GroupsService.Infrastructure.Outbox;

// Salje neposlate outbox poruke na broker, po redosledu nastanka. Poruka je oznacena kao
// poslata tek kad broker potvrdi prijem; ako slanje ne uspe, ostaje za sledeci krug
// (isporuka je "bar jednom", potrosaci podnose duplikate preko EventId-a).
// FOR UPDATE SKIP LOCKED: vise instanci API-ja ne salje istu poruku istovremeno.
public sealed class OutboxDispatcher : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Retention = TimeSpan.FromDays(7);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMessagePublisher _publisher;
    private readonly ILogger<OutboxDispatcher> _logger;
    private DateTime _lastCleanup = DateTime.MinValue;

    public OutboxDispatcher(IServiceScopeFactory scopeFactory, IMessagePublisher publisher, ILogger<OutboxDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _publisher = publisher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var (sent, failed) = await DispatchBatchAsync(stoppingToken);
                if (failed)
                    await Task.Delay(ErrorDelay, stoppingToken);
                else if (sent == 0)
                {
                    await CleanupIfDueAsync(stoppingToken);
                    await Task.Delay(PollInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Outbox dispatch failed.");
                await Task.Delay(ErrorDelay, stoppingToken).ContinueWith(_ => { });
            }
        }
    }

    private async Task<(int Sent, bool Failed)> DispatchBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GroupDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var batch = await db.OutboxMessages
            .FromSqlRaw(
                "SELECT * FROM outbox_messages WHERE processed_at IS NULL " +
                "ORDER BY occurred_at LIMIT {0} FOR UPDATE SKIP LOCKED", BatchSize)
            .ToListAsync(cancellationToken);

        var sent = 0;
        var failed = false;
        foreach (var message in batch)
        {
            try
            {
                using var payload = JsonDocument.Parse(message.Payload);
                await _publisher.PublishAsync(
                    new EventEnvelope(message.Id, message.Type, message.OccurredAt, payload.RootElement.Clone()),
                    cancellationToken);
                message.ProcessedAt = DateTime.UtcNow;
                sent++;
            }
            catch (Exception e) when (!cancellationToken.IsCancellationRequested)
            {
                // Redosled je bitan, pa se posle prve greske ostatak salje u sledecem krugu.
                message.Attempts++;
                message.LastError = e.Message.Length > 2000 ? e.Message[..2000] : e.Message;
                _logger.LogWarning(e, "Publishing outbox message {MessageId} ({Type}) failed (attempt {Attempts}).",
                    message.Id, message.Type, message.Attempts);
                failed = true;
                break;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (sent, failed);
    }

    // Poslate poruke se cuvaju nedelju dana (za proveru sta je otislo), pa se brisu.
    private async Task CleanupIfDueAsync(CancellationToken cancellationToken)
    {
        if (DateTime.UtcNow - _lastCleanup < CleanupInterval)
            return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GroupDbContext>();
        var cutoff = DateTime.UtcNow - Retention;
        var deleted = await db.OutboxMessages
            .Where(m => m.ProcessedAt != null && m.ProcessedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        _lastCleanup = DateTime.UtcNow;
        if (deleted > 0)
            _logger.LogInformation("Deleted {Count} processed outbox messages older than {Days} days.", deleted, Retention.Days);
    }
}
