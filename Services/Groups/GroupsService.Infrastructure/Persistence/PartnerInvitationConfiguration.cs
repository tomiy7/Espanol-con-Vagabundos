using GroupsService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GroupsService.Infrastructure.Persistence;

public class PartnerInvitationConfiguration : IEntityTypeConfiguration<PartnerInvitation>
{
    public void Configure(EntityTypeBuilder<PartnerInvitation> builder)
    {
        builder.ToTable("partner_invitations");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.InviterEnrollmentId).HasColumnName("inviter_enrollment_id").IsRequired();
        builder.Property(i => i.InviterUserId).HasColumnName("inviter_user_id").IsRequired();
        builder.Property(i => i.CourseId).HasColumnName("course_id").IsRequired();
        builder.Property(i => i.InviteeUserId).HasColumnName("invitee_user_id").IsRequired();

        builder.Property(i => i.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(i => i.AttemptNumber).HasColumnName("attempt_number").IsRequired();

        builder.Property(i => i.Token).HasColumnName("token").HasMaxLength(64).IsRequired();
        builder.HasIndex(i => i.Token).IsUnique();

        builder.Property(i => i.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(i => i.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(i => i.RespondedAt).HasColumnName("responded_at");

        builder.HasIndex(i => new { i.InviterEnrollmentId, i.InviteeUserId });

        // Isto kao kod upisa (xmin): prihvatanje i odbijanje istog poziva u isto vreme
        // ne mogu oba da prodju.
        builder.Property<uint>(ConcurrencyTokens.RowVersion).IsRowVersion();

        builder.Ignore(i => i.DomainEvents);
        builder.Ignore(i => i.IsExpired);

        builder.Ignore(i => i.CreatedBy);
        builder.Ignore(i => i.CreatedDate);
        builder.Ignore(i => i.LastModifiedBy);
        builder.Ignore(i => i.LastModifiedDate);
    }
}