using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RushOrder.Domain.Entities;

namespace RushOrder.Infrastructure.Persistence.Configurations;

public sealed class ExperimentResultConfiguration : IEntityTypeConfiguration<ExperimentResult>
{
    public void Configure(EntityTypeBuilder<ExperimentResult> builder)
    {
        builder.ToTable("experiment_results");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.ExperimentKey).HasMaxLength(100).IsRequired();
        builder.Property(r => r.DeviceFingerprint).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Variant).HasConversion<string>().HasMaxLength(1);
        builder.Property(r => r.EventType).HasConversion<string>().HasMaxLength(30);
        builder.Property(r => r.CartTotal).HasPrecision(18, 2);

        // Append-only log — always queried/aggregated by (restaurant, experiment key), see
        // ExperimentRepository.GetResultsAsync.
        builder.HasIndex(r => new { r.TenantId, r.RestaurantId, r.ExperimentKey, r.EventType });
    }
}
