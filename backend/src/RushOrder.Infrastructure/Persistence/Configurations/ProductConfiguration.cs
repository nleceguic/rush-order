using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RushOrder.Domain.Entities;
using RushOrder.Domain.Enums;

namespace RushOrder.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(1000);
        builder.Property(p => p.ImageUrl).HasMaxLength(500);
        builder.Property(p => p.PreparationMinutes);

        builder.OwnsOne(p => p.Price, m =>
        {
            m.Property(x => x.Amount).HasColumnName("price_amount").HasPrecision(18, 2);
            m.Property(x => x.Currency).HasColumnName("price_currency").HasMaxLength(3);
        });

        // PostgreSQL text[] — backing field is List<string>, natively supported by Npgsql
        builder.Property(p => p.Allergens)
            .HasField("_allergens")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnType("text[]")
            .HasColumnName("allergens")
            .Metadata.SetValueComparer(new ValueComparer<IReadOnlyList<string>>(
                (a, b) => a != null && b != null && a.SequenceEqual(b),
                v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
                v => (IReadOnlyList<string>)v.ToList()));

        // Tags stored as text[] with enum↔string conversion via backing field
        builder.Property(p => p.Tags)
            .HasField("_tags")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnType("text[]")
            .HasColumnName("tags")
            .HasConversion(
                v => v.Select(t => t.ToString()).ToArray(),
                v => (IReadOnlyList<ProductTag>)v.Select(Enum.Parse<ProductTag>).ToList())
            .Metadata.SetValueComparer(new ValueComparer<IReadOnlyList<ProductTag>>(
                (a, b) => a != null && b != null && a.SequenceEqual(b),
                v => v.Aggregate(0, (h, t) => HashCode.Combine(h, t.GetHashCode())),
                v => (IReadOnlyList<ProductTag>)v.ToList()));

        builder.HasIndex(p => new { p.CategoryId, p.IsAvailable, p.SortOrder });
    }
}
