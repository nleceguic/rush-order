using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RushOrder.Domain.Entities;

namespace RushOrder.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.OwnsOne(u => u.Email, e =>
        {
            e.Property(x => x.Value)
                .HasColumnName("email")
                .HasMaxLength(200)
                .IsRequired();
            e.HasIndex(x => x.Value)
                .IsUnique()
                .HasDatabaseName("ix_users_email");
        });

        builder.Property(u => u.PasswordHash).HasMaxLength(200).IsRequired();
        builder.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.LastName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        builder.Property(u => u.MfaSecret).HasMaxLength(200);
        builder.Property(u => u.PendingMfaSecret).HasMaxLength(500);
        builder.Property(u => u.MfaBackupCodesJson).HasColumnType("text");
        builder.Ignore(u => u.MfaEnabled);

        // PostgreSQL native uuid[] — use backing field so EF sees List<Guid>
        builder.Property(u => u.RestaurantIds)
            .HasField("_restaurantIds")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnType("uuid[]")
            .HasColumnName("restaurant_ids")
            .Metadata.SetValueComparer(new ValueComparer<IReadOnlyList<Guid>>(
                (a, b) => a != null && b != null && a.SequenceEqual(b),
                v => v.Aggregate(0, (h, g) => HashCode.Combine(h, g.GetHashCode())),
                v => (IReadOnlyList<Guid>)v.ToList()));
    }
}
