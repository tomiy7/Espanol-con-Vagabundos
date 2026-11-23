using System.Text.Json;
using GroupsService.Domain;
using GroupsService.Domain.Entities;
using GroupsService.Infrastructure.Outbox;
using GroupsService.Infrastructure.Persistence;
using Messaging;
using Microsoft.EntityFrameworkCore;

namespace GroupsService.Infrastructure.Data;

public class GroupDbContext : DbContext
{
    public GroupDbContext(DbContextOptions<GroupDbContext> options) : base(options) { }
    
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<PartnerInvitation> PartnerInvitations => Set<PartnerInvitation>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new EnrollmentConfiguration());
        modelBuilder.ApplyConfiguration(new GroupConfiguration());
        modelBuilder.ApplyConfiguration(new PartnerInvitationConfiguration());
        modelBuilder.ApplyConfiguration(new AuditEntryConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
    }

    // Domenski dogadjaji agregata se pre snimanja prevode u outbox poruke, pa idu u ISTU
    // transakciju kao promena koja ih je izazvala (sve ili nista). Dogadjaji se brisu tek
    // posle uspesnog snimanja; ako snimanje padne, dodate poruke se uklanjaju iz konteksta.
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        var (aggregates, messages) = AddOutboxMessages();
        try
        {
            var result = base.SaveChanges(acceptAllChangesOnSuccess);
            ClearDomainEvents(aggregates);
            return result;
        }
        catch
        {
            DetachOutboxMessages(messages);
            throw;
        }
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var (aggregates, messages) = AddOutboxMessages();
        try
        {
            var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            ClearDomainEvents(aggregates);
            return result;
        }
        catch
        {
            DetachOutboxMessages(messages);
            throw;
        }
    }

    private (List<AggregateRoot> Aggregates, List<OutboxMessage> Messages) AddOutboxMessages()
    {
        var aggregates = ChangeTracker.Entries<AggregateRoot>()
            .Select(entry => entry.Entity)
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToList();

        var messages = new List<OutboxMessage>();
        foreach (var aggregate in aggregates)
        foreach (var domainEvent in aggregate.DomainEvents)
        {
            var integrationEvent = IntegrationEventMapper.Map(aggregate, domainEvent);
            if (integrationEvent is null) continue;

            messages.Add(new OutboxMessage
            {
                Id = integrationEvent.EventId,
                Type = EventEnvelope.TypeOf(integrationEvent.GetType()),
                Payload = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), EventEnvelope.JsonOptions),
                OccurredAt = integrationEvent.OccurredAt
            });
        }

        OutboxMessages.AddRange(messages);
        return (aggregates, messages);
    }

    private static void ClearDomainEvents(List<AggregateRoot> aggregates)
    {
        foreach (var aggregate in aggregates)
            aggregate.ClearDomainEvents();
    }

    private void DetachOutboxMessages(List<OutboxMessage> messages)
    {
        foreach (var message in messages)
            Entry(message).State = EntityState.Detached;
    }
}