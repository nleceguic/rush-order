using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RushOrder.Domain.Entities;

namespace RushOrder.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Slug).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.TrialEndsAt);
        builder.Property(t => t.StripeCustomerId).HasMaxLength(200);

        builder.HasIndex(t => t.Slug).IsUnique();

        builder.OwnsOne(t => t.Settings, s =>
        {
            s.ToJson();
            s.Property(x => x.DefaultLanguage).HasMaxLength(10);
            s.Property(x => x.BrandColor).HasMaxLength(7);
            s.Property(x => x.LogoUrl).HasMaxLength(500);
        });

        builder.OwnsOne(t => t.BillingInfo, b =>
        {
            b.ToJson();
            b.Property(x => x.CompanyName).HasMaxLength(200);
            b.Property(x => x.TaxId).HasMaxLength(50);
            b.Property(x => x.Country).HasMaxLength(2);
        });
    }
}
