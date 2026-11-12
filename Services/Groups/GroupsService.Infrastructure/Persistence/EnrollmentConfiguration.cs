using GroupsService.Domain.Entities;
using GroupsService.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GroupsService.Infrastructure.Persistence;

public class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.ToTable("enrollments");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(e => e.CourseId).HasColumnName("course_id").IsRequired();

        builder.Property(e => e.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.ProfessorId).HasColumnName("professor_id");
        builder.Property(e => e.GroupId).HasColumnName("group_id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.ActivatedAt).HasColumnName("activated_at");
        builder.Property(e => e.CancelledAt).HasColumnName("cancelled_at");
        builder.Property(e => e.CompletedAt).HasColumnName("completed_at");

        // Zamrzavanje: trenutak kad je zamrznut i ukupno vreme koje je vec proteklo u
        // zamrznutom stanju (zaustavlja sat za otvaranje lekcija).
        builder.Property(e => e.FrozenAt).HasColumnName("frozen_at");
        builder.Property(e => e.TotalFrozenTime).HasColumnName("total_frozen_time").IsRequired();

        // Najcesci upiti: "sve prijave korisnika", "ko je u ovoj grupi"
        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => e.GroupId);

        // Jedan neotkazan upis po korisniku i kursu - baza garantuje pravilo i kad dva
        // zahteva stignu istovremeno (provera u EnrollmentGuard to ne moze sama).
        builder.HasIndex(e => new { e.UserId, e.CourseId })
            .IsUnique()
            .HasFilter("\"status\" <> 'Cancelled'");

        // Napredak - owned kolekcija zavrsenih lekcija. Slozeni kljuc (enrollment,
        // lekcija) je i garancija idempotentnosti: dupli LessonCompleted event ne
        // moze da upise istu lekciju dvaput ni u trci dva paralelna handlera.
        builder.OwnsMany(e => e.CompletedLessons, l =>
        {
            l.ToTable("enrollment_completed_lessons");
            l.WithOwner().HasForeignKey("enrollment_id");

            l.Property(x => x.LessonId).HasColumnName("lesson_id").ValueGeneratedNever();
            l.Property(x => x.CompletedAt).HasColumnName("completed_at").IsRequired();

            l.HasKey("enrollment_id", nameof(CompletedLesson.LessonId));
        });

        builder.Metadata.FindNavigation(nameof(Enrollment.CompletedLessons))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(e => e.CompletedLessonCount);
        builder.Ignore(e => e.DomainEvents);

        builder.Ignore(e => e.CreatedBy);
        builder.Ignore(e => e.LastModifiedBy);
        builder.Ignore(e => e.LastModifiedDate);
        // CreatedDate (iz Entity base) se preklapa sa CreatedAt - mapiramo samo CreatedAt.
        builder.Ignore(e => e.CreatedDate);
    }
}