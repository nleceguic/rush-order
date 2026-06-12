using Microsoft.EntityFrameworkCore;
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
            e.Property(x => x.Value).HasColumnName("email").HasMaxLength(200).IsRequired();
        });

        builder.Property(u => u.PasswordHash).HasMaxLength(200).IsRequired();
        builder.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.LastName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        builder.Property(u => u.MfaSecret).HasMaxLength(200);

        // PostgreSQL native uuid[] array column
        builder.Property(u => u.RestaurantIds)
            .HasColumnType("uuid[]")
            .HasConversion(
                v => v.ToArray(),
                v => v.ToList())
            .Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<IReadOnlyList<Guid>>(
                (a, b) => a != null && b != null && a.SequenceEqual(b),
                v => v.Aggregate(0, (h, g) => HashCode.Combine(h, g.GetHashCode())),
                v => v.ToList()));

        builder.HasIndex("email").IsUnique().HasFilter("email IS NOT NULL");
    }
}
