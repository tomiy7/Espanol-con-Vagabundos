using GroupsService.Domain.Entities;
using GroupsService.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GroupsService.Infrastructure.Persistence;

public class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.ToTable("groups");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id).HasColumnName("id");
        builder.Property(g => g.CourseId).HasColumnName("course_id").IsRequired();

        builder.Property(g => g.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(g => g.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(g => g.FormationWindowEndsAt).HasColumnName("formation_window_ends_at");
        builder.Property(g => g.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(g => g.StartedAt).HasColumnName("started_at");
        builder.Property(g => g.UnlockStartsAt).HasColumnName("unlock_starts_at");
        builder.Property(g => g.PlannedStartAt).HasColumnName("planned_start_at");
        builder.Property(g => g.ConfirmedAt).HasColumnName("confirmed_at");

        // Optimisticka konkurentnost za zadnje mesto u grupi (vidi Group.Version).
        builder.Property(g => g.Version).HasColumnName("version").IsConcurrencyToken();

        builder.HasIndex(g => new { g.CourseId, g.Status });
        
        // Clanovi - owned kolekcija, Entity (ima Id) radi lakseg EF mapiranja
        builder.OwnsMany(g => g.Members, m =>
        {
            m.ToTable("group_members");
            m.WithOwner().HasForeignKey("group_id");

            m.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            m.HasKey(x => x.Id);

            m.Property(x => x.EnrollmentId).HasColumnName("enrollment_id").IsRequired();
            m.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            m.Property(x => x.PaymentPlan).HasColumnName("payment_plan")
                .HasConversion<string>().HasMaxLength(20).IsRequired();
            m.Property(x => x.JoinedAt).HasColumnName("joined_at").IsRequired();

            // Indeks se zadaje po imenu SVOJSTVA (EnrollmentId), ne po imenu kolone;
            // "group_id" je shadow svojstvo definisano preko HasForeignKey iznad.
            m.HasIndex("group_id", nameof(GroupMember.EnrollmentId)).IsUnique();

            m.Ignore(x => x.CreatedBy);
            m.Ignore(x => x.CreatedDate);
            m.Ignore(x => x.LastModifiedBy);
            m.Ignore(x => x.LastModifiedDate);
        });
        
        builder.Metadata.FindNavigation(nameof(Group.Members))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // Slotovi - 3 za Pair/Group, 1 za Individual (vidi Group.CreateInternal), owned kolekcija
        builder.OwnsMany(g => g.Slots, s =>
        {
            s.ToTable("group_slots");
            s.WithOwner().HasForeignKey("group_id");

            s.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            s.HasKey(x => x.Id);

            s.Property(x => x.SlotIndex).HasColumnName("slot_index").IsRequired();
            s.Property(x => x.DayOfWeek).HasColumnName("day_of_week");
            s.Property(x => x.Time).HasColumnName("time");
            s.Property(x => x.ProfessorId).HasColumnName("professor_id");

            s.HasIndex("group_id", nameof(Slot.SlotIndex)).IsUnique();

            s.Ignore(x => x.CreatedBy);
            s.Ignore(x => x.CreatedDate);
            s.Ignore(x => x.LastModifiedBy);
            s.Ignore(x => x.LastModifiedDate);
        });

        builder.Metadata.FindNavigation(nameof(Group.Slots))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(g => g.DomainEvents);
        builder.Ignore(g => g.MemberCount);
        builder.Ignore(g => g.MinSize);
        builder.Ignore(g => g.MaxSize);
        builder.Ignore(g => g.FormationWindowExpired);

        builder.Ignore(g => g.CreatedBy);
        builder.Ignore(g => g.CreatedDate);
        builder.Ignore(g => g.LastModifiedBy);
        builder.Ignore(g => g.LastModifiedDate);
    }
}