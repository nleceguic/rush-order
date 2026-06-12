using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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

        var listComparer = new ValueComparer<IReadOnlyList<string>>(
            (a, b) => a != null && b != null && a.SequenceEqual(b),
            v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
            v => v.ToList());

        var tagComparer = new ValueComparer<IReadOnlyList<ProductTag>>(
            (a, b) => a != null && b != null && a.SequenceEqual(b),
            v => v.Aggregate(0, (h, t) => HashCode.Combine(h, t.GetHashCode())),
            v => v.ToList());

        // PostgreSQL native text[] and smallint[] arrays
        builder.Property(p => p.Allergens)
            .HasColumnType("text[]")
            .HasConversion(v => v.ToArray(), v => (IReadOnlyList<string>)v.ToList())
            .Metadata.SetValueComparer(listComparer);

        builder.Property(p => p.Tags)
            .HasColumnType("text[]")
            .HasConversion(
                v => v.Select(t => t.ToString()).ToArray(),
                v => (IReadOnlyList<ProductTag>)v.Select(Enum.Parse<ProductTag>).ToList())
            .Metadata.SetValueComparer(tagComparer);

        builder.HasIndex(p => new { p.CategoryId, p.IsAvailable, p.SortOrder });
    }
}
