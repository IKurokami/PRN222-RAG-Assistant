using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PRN222.RagAssistant.Domain.Entities;

namespace PRN222.RagAssistant.Data.Configurations;

public sealed class PaymentOrderConfiguration : IEntityTypeConfiguration<PaymentOrder>
{
    public void Configure(EntityTypeBuilder<PaymentOrder> builder)
    {
        builder.ToTable("PaymentOrders");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Provider)
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(x => x.ExternalOrderId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(x => x.Amount)
            .IsRequired();
        builder.Property(x => x.Currency)
            .HasMaxLength(8)
            .IsRequired();
        builder.Property(x => x.Status)
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(x => x.ExternalResponseCode)
            .HasMaxLength(32);
        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(2000);
        builder.Property(x => x.MetadataJson)
            .HasMaxLength(4000);
        builder.Property(x => x.ExternalTransactionNo)
            .HasMaxLength(128);
        builder.Property(x => x.BankCode)
            .HasMaxLength(64);
        builder.Property(x => x.CardType)
            .HasMaxLength(64);

        builder.HasIndex(x => x.ExternalOrderId)
            .IsUnique();
        builder.HasIndex(x => new { x.UserId, x.Status });

        builder.HasOne<Subject>()
            .WithMany()
            .HasForeignKey(x => x.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
