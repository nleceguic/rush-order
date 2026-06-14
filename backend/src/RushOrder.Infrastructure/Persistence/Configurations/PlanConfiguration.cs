using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RushOrder.Domain.Entities;

namespace RushOrder.Infrastructure.Persistence.Configurations;

public sealed class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.ToTable("plans");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Name).HasMaxLength(100).IsRequired();
        builder.Property(p => p.IsActive);
        builder.Property(p => p.MaxTables);
        builder.Property(p => p.MaxUsers);
        builder.Property(p => p.MaxRestaurants);
        builder.Property(p => p.StripePriceIdMonthly).HasMaxLength(200);
        builder.Property(p => p.StripePriceIdYearly).HasMaxLength(200);

        builder.OwnsOne(p => p.PriceMonthly, m =>
        {
            m.Property(x => x.Amount).HasColumnName("price_monthly_amount").HasPrecision(18, 2);
            m.Property(x => x.Currency).HasColumnName("price_monthly_currency").HasMaxLength(3);
        });

        builder.OwnsOne(p => p.PriceYearly, m =>
        {
            m.Property(x => x.Amount).HasColumnName("price_yearly_amount").HasPrecision(18, 2);
            m.Property(x => x.Currency).HasColumnName("price_yearly_currency").HasMaxLength(3);
        });

        builder.Property(p => p.Features)
            .HasField("_features")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnType("text[]")
            .HasColumnName("features")
            .Metadata.SetValueComparer(new ValueComparer<IReadOnlyList<string>>(
                (a, b) => a != null && b != null && a.SequenceEqual(b),
                v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
                v => (IReadOnlyList<string>)v.ToList()));

        builder.HasIndex(p => p.Name).IsUnique();
    }
}
