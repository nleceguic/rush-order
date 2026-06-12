using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RushOrder.Domain.Entities;

namespace RushOrder.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.OwnsOne(c => c.Email, e =>
        {
            e.Property(x => x.Value).HasColumnName("email").HasMaxLength(200);
        });

        builder.Property(c => c.Phone).HasMaxLength(30);
        builder.Property(c => c.FirstName).HasMaxLength(100);
        builder.Property(c => c.LastName).HasMaxLength(100);
        builder.Property(c => c.DeviceFingerprint).HasMaxLength(100).IsRequired();
        builder.Property(c => c.LoyaltyLevel).HasConversion<string>().HasMaxLength(20);

        builder.OwnsOne(c => c.TotalSpent, m =>
        {
            m.Property(x => x.Amount).HasColumnName("total_spent_amount").HasPrecision(18, 2);
            m.Property(x => x.Currency).HasColumnName("total_spent_currency").HasMaxLength(3);
        });

        builder.OwnsOne(c => c.Preferences, p => p.ToJson());
    }
}
