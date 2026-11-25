using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NotificationsService.API.Data;

public static class NotificationStatus
{
    public const string Pending = "Pending";   // zapoceto slanje (ili je slanje palo, ponavlja se)
    public const string Sent = "Sent";
    public const string Skipped = "Skipped";   // namerno nije poslato (npr. korisnik nema mejl)
}

// Trag svakog mejla. (EventId, primalac, sablon) je jedinstven: ista poruka stigla dvaput
// (isporuka je "bar jednom") ne salje drugi mejl.
public class SentNotification
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public Guid RecipientUserId { get; set; }
    public string Template { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Status { get; set; } = NotificationStatus.Pending;
    public int Attempts { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
}

public class NotificationsDbContext : DbContext
{
    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : base(options) { }

    public DbSet<SentNotification> SentNotifications => Set<SentNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SentNotification>(b =>
        {
            b.ToTable("sent_notifications");
            b.HasKey(n => n.Id);
            b.Property(n => n.Id).HasColumnName("id");
            b.Property(n => n.EventId).HasColumnName("event_id");
            b.Property(n => n.EventType).HasColumnName("event_type").HasMaxLength(100).IsRequired();
            b.Property(n => n.RecipientUserId).HasColumnName("recipient_user_id");
            b.Property(n => n.Template).HasColumnName("template").HasMaxLength(100).IsRequired();
            b.Property(n => n.Email).HasColumnName("email").HasMaxLength(320);
            b.Property(n => n.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
            b.Property(n => n.Attempts).HasColumnName("attempts");
            b.Property(n => n.Error).HasColumnName("error").HasMaxLength(2000);
            b.Property(n => n.CreatedAt).HasColumnName("created_at");
            b.Property(n => n.SentAt).HasColumnName("sent_at");

            b.HasIndex(n => new { n.EventId, n.RecipientUserId, n.Template }).IsUnique();
            b.HasIndex(n => n.RecipientUserId);
        });
    }
}

// Za "dotnet ef" komande (migracije) bez pokretanja servisa.
public class NotificationsDbContextFactory : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    public NotificationsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
                               ?? "Host=localhost;Port=5438;Database=notificationsdb;Username=postgres;Password=changeme";
        return new NotificationsDbContext(
            new DbContextOptionsBuilder<NotificationsDbContext>().UseNpgsql(connectionString).Options);
    }
}
