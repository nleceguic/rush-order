using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RushOrder.Domain.Entities;

namespace RushOrder.Infrastructure.Persistence.Configurations;

public sealed class ProductPairingRuleConfiguration : IEntityTypeConfiguration<ProductPairingRule>
{
    public void Configure(EntityTypeBuilder<ProductPairingRule> builder)
    {
        builder.ToTable("product_pairing_rules");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.HasIndex(r => new { r.TenantId, r.RestaurantId, r.SourceProductId, r.IsActive });
        builder.HasIndex(r => new { r.SourceProductId, r.TargetProductId }).IsUnique();
    }
}
