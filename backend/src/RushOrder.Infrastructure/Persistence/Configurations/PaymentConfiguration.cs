using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RushOrder.Domain.Entities;

namespace RushOrder.Infrastructure.Persistence.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Provider).HasMaxLength(50).IsRequired();
        builder.Property(p => p.ProviderPaymentId).HasMaxLength(200);
        builder.Property(p => p.Method).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.ReceiptUrl).HasMaxLength(500);
        builder.Property(p => p.FailureReason).HasMaxLength(500);

        builder.OwnsOne(p => p.Amount, m =>
        {
            m.Property(x => x.Amount).HasColumnName("amount_value").HasPrecision(18, 2);
            m.Property(x => x.Currency).HasColumnName("amount_currency").HasMaxLength(3);
        });

        builder.HasIndex(p => new { p.TenantId, p.OrderId });
        builder.HasIndex(p => p.ProviderPaymentId).IsUnique().HasFilter("\"ProviderPaymentId\" IS NOT NULL");
    }
}
