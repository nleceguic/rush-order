using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RushOrder.Domain.Entities;

namespace RushOrder.Infrastructure.Persistence.Configurations;

/*
 * PostgreSQL range partitioning by year (run after initial migration):
 *
 * ALTER TABLE orders RENAME TO orders_default;
 * CREATE TABLE orders (LIKE orders_default INCLUDING ALL) PARTITION BY RANGE (created_at);
 * CREATE TABLE orders_2024 PARTITION OF orders FOR VALUES FROM ('2024-01-01') TO ('2025-01-01');
 * CREATE TABLE orders_2025 PARTITION OF orders FOR VALUES FROM ('2025-01-01') TO ('2026-01-01');
 * CREATE TABLE orders_2026 PARTITION OF orders FOR VALUES FROM ('2026-01-01') TO ('2027-01-01');
 * -- Create a yearly cron/script to add next-year partition each January.
 */
public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();

        builder.Property(o => o.OrderNumber).HasMaxLength(20).IsRequired();
        builder.Property(o => o.Source).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.Notes).HasMaxLength(500);
        builder.Property(o => o.CancellationReason).HasMaxLength(500);
        builder.Property(o => o.TaxRate).HasPrecision(5, 4);

        builder.OwnsOne(o => o.Subtotal, m =>
        {
            m.Property(x => x.Amount).HasColumnName("subtotal_amount").HasPrecision(18, 2);
            m.Property(x => x.Currency).HasColumnName("subtotal_currency").HasMaxLength(3);
        });
        builder.OwnsOne(o => o.TaxAmount, m =>
        {
            m.Property(x => x.Amount).HasColumnName("tax_amount").HasPrecision(18, 2);
            m.Property(x => x.Currency).HasColumnName("tax_currency").HasMaxLength(3);
        });
        builder.OwnsOne(o => o.DiscountAmount, m =>
        {
            m.Property(x => x.Amount).HasColumnName("discount_amount").HasPrecision(18, 2);
            m.Property(x => x.Currency).HasColumnName("discount_currency").HasMaxLength(3);
        });
        builder.OwnsOne(o => o.TipAmount, m =>
        {
            m.Property(x => x.Amount).HasColumnName("tip_amount").HasPrecision(18, 2);
            m.Property(x => x.Currency).HasColumnName("tip_currency").HasMaxLength(3);
        });
        builder.OwnsOne(o => o.Total, m =>
        {
            m.Property(x => x.Amount).HasColumnName("total_amount").HasPrecision(18, 2);
            m.Property(x => x.Currency).HasColumnName("total_currency").HasMaxLength(3);
        });

        // Items stored as JSONB owned collection
        builder.OwnsMany(o => o.Items, item =>
        {
            item.ToJson("items");
            item.Property(i => i.Id).IsRequired();
            item.Property(i => i.Name).HasMaxLength(200);
            item.Property(i => i.Notes).HasMaxLength(300);
            item.OwnsOne(i => i.UnitPrice, m =>
            {
                m.Property(x => x.Amount).HasColumnName("unit_price_amount").HasPrecision(18, 2);
                m.Property(x => x.Currency).HasColumnName("unit_price_currency").HasMaxLength(3);
            });
        });

        builder.HasIndex(o => new { o.TenantId, o.RestaurantId, o.Status, o.CreatedAt });
    }
}
