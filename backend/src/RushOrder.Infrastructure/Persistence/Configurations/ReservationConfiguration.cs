using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RushOrder.Domain.Entities;

namespace RushOrder.Infrastructure.Persistence.Configurations;

public sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("reservations");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.GuestName).HasMaxLength(200).IsRequired();
        builder.Property(r => r.GuestEmail).HasMaxLength(200);
        builder.Property(r => r.GuestPhone).HasMaxLength(50).IsRequired();
        builder.Property(r => r.ConfirmationCode).HasMaxLength(10).IsRequired();
        builder.Property(r => r.Notes).HasMaxLength(1000);
        builder.Property(r => r.CancellationReason).HasMaxLength(500);
        builder.Property(r => r.PreOrder).HasColumnType("jsonb");
        builder.Property(r => r.DurationMinutes).HasDefaultValue(90);

        builder.HasIndex(r => new { r.TenantId, r.RestaurantId, r.ReservedAt });
        builder.HasIndex(r => r.ConfirmationCode).IsUnique();
    }
}
