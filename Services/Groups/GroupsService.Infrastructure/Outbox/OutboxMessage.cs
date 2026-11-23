namespace GroupsService.Infrastructure.Outbox;

// Dogadjaj koji ceka slanje. Upisuje se u ISTOJ transakciji kao poslovna promena, pa
// dogadjaj postoji ako i samo ako je promena snimljena; salje ga OutboxDispatcher.
public class OutboxMessage
{
    public Guid Id { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Payload { get; init; } = string.Empty;
    public DateTime OccurredAt { get; init; }
    public DateTime? ProcessedAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}
