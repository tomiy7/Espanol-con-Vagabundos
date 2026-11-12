using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GroupsService.Infrastructure.Data;

namespace GroupsService.Infrastructure.Persistence;

public class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_entries");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(a => a.ActorUserId).HasColumnName("actor_user_id").IsRequired();
        builder.Property(a => a.Action).HasColumnName("action").HasMaxLength(50).IsRequired();
        builder.Property(a => a.EntityType).HasColumnName("entity_type").HasMaxLength(30).IsRequired();
        builder.Property(a => a.EntityId).HasColumnName("entity_id").IsRequired();
        builder.Property(a => a.Details).HasColumnName("details").HasMaxLength(500);
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();

        // Upiti: "sta se desavalo sa ovim entitetom", "sta je radio ovaj korisnik", najnovije prvo
        builder.HasIndex(a => a.EntityId);
        builder.HasIndex(a => a.ActorUserId);
        builder.HasIndex(a => a.CreatedAt);
    }
}