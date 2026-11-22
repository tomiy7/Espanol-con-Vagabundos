using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentsService.Domain.Entities;

namespace PaymentsService.Infrastructure.Persistence;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(p => p.CourseId).HasColumnName("course_id");
        
        builder.Property(p => p.ProductType)
            .HasColumnName("product_type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.ReferenceNumber)
            .HasColumnName("reference_number")
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(p => p.ReferenceNumber).IsUnique();

        builder.Property(p => p.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(p => p.ConfirmedAt).HasColumnName("confirmed_at");
        builder.Property(p => p.ConfirmedByUserId).HasColumnName("confirmed_by_user_id");
        
        builder.OwnsOne(p => p.Amount, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("amount")
                .HasColumnType("decimal(10,2)")
                .IsRequired();

            money.Property(m => m.Currency)
                .HasColumnName("currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Navigation(p => p.Amount).IsRequired();
    }
}