using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RushOrder.Domain.Entities;

namespace RushOrder.Infrastructure.Persistence.Configurations;

public sealed class RestaurantConfiguration : IEntityTypeConfiguration<Restaurant>
{
    public void Configure(EntityTypeBuilder<Restaurant> builder)
    {
        builder.ToTable("restaurants");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Name).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Address).HasMaxLength(500).IsRequired();
        builder.Property(r => r.Phone).HasMaxLength(30).IsRequired();
        builder.Property(r => r.Email).HasMaxLength(200).IsRequired();
        builder.Property(r => r.LogoUrl).HasMaxLength(500);
        builder.Property(r => r.CoverUrl).HasMaxLength(500);
        builder.Property(r => r.Timezone).HasMaxLength(50);
        builder.Property(r => r.Currency).HasMaxLength(3);
        builder.Property(r => r.TaxRate).HasPrecision(5, 4);

        builder.Property(r => r.StripeAccountId).HasMaxLength(100);

        builder.HasIndex(r => new { r.TenantId, r.IsActive });

        builder.OwnsOne(r => r.Settings, s => s.ToJson());
    }
}
