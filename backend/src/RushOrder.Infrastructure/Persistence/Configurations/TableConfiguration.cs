using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RushOrder.Domain.Entities;

namespace RushOrder.Infrastructure.Persistence.Configurations;

public sealed class TableConfiguration : IEntityTypeConfiguration<Table>
{
    public void Configure(EntityTypeBuilder<Table> builder)
    {
        builder.ToTable("tables");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Name).HasMaxLength(50).IsRequired();
        builder.Property(t => t.Zone).HasMaxLength(100);
        builder.Property(t => t.QrCode).HasMaxLength(20).IsRequired();
        builder.Property(t => t.QrUrl).HasMaxLength(500).IsRequired();
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(t => t.QrCode).IsUnique();
        builder.HasIndex(t => new { t.RestaurantId, t.Status });
    }
}
