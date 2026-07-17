using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RushOrder.Domain.Entities;

namespace RushOrder.Infrastructure.Persistence.Configurations;

public sealed class OrderRatingConfiguration : IEntityTypeConfiguration<OrderRating>
{
    public void Configure(EntityTypeBuilder<OrderRating> builder)
    {
        builder.ToTable("order_ratings");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Comment).HasMaxLength(500);

        builder.HasIndex(r => r.OrderId).IsUnique();
        builder.HasIndex(r => new { r.TenantId, r.RestaurantId, r.CreatedAt });
    }
}
