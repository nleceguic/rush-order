using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RushOrder.Domain.Entities;

namespace RushOrder.Infrastructure.Persistence.Configurations;

public sealed class DemandForecastConfiguration : IEntityTypeConfiguration<DemandForecast>
{
    public void Configure(EntityTypeBuilder<DemandForecast> builder)
    {
        builder.ToTable("demand_forecasts");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();

        builder.Property(f => f.PredictedQuantity).HasPrecision(10, 2);
        builder.Property(f => f.ConfidenceLevel).HasConversion<string>().HasMaxLength(10);

        // The nightly job deletes+reinserts a restaurant's whole 7-day window,
        // so lookups are always by this exact combination.
        builder.HasIndex(f => new { f.TenantId, f.RestaurantId, f.ForecastDate, f.ProductId, f.ForecastHour })
            .IsUnique()
            .HasDatabaseName("IX_demand_forecasts_lookup");
    }
}
