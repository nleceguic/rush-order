using Microsoft.EntityFrameworkCore;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;
using RushOrder.Domain.Enums;

namespace RushOrder.Infrastructure.Persistence.Repositories;

public sealed class ReservationRepository : Repository<Reservation>, IReservationRepository
{
    private readonly AppDbContext _context;

    public ReservationRepository(AppDbContext context) : base(context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Reservation>> GetByDateAsync(
        Guid restaurantId,
        DateTimeOffset date,
        ReservationStatus? status,
        CancellationToken cancellationToken = default)
    {
        var dayStart = new DateTimeOffset(date.Date, date.Offset);
        var dayEnd = dayStart.AddDays(1);
        var query = DbSet.AsNoTracking()
            .Where(r => r.RestaurantId == restaurantId
                && r.ReservedAt >= dayStart
                && r.ReservedAt < dayEnd);
        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);
        return await query.OrderBy(r => r.ReservedAt).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Reservation>> GetUpcomingAsync(
        Guid restaurantId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
        => await DbSet.AsNoTracking()
            .Where(r => r.RestaurantId == restaurantId
                && r.ReservedAt >= from
                && r.ReservedAt <= to
                && r.Status != ReservationStatus.Cancelled
                && r.Status != ReservationStatus.NoShow)
            .OrderBy(r => r.ReservedAt)
            .ToListAsync(cancellationToken);

    public async Task<bool> HasAvailabilityAsync(
        Guid restaurantId,
        int partySize,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        // Scoped by restaurantId, so the tenant filter is bypassed: anonymous self-booking
        // has no tenant claim and would otherwise always see zero tables and reservations.
        var capableTablesCount = await _context.Tables
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(t => t.RestaurantId == restaurantId && t.Capacity >= partySize, cancellationToken);

        if (capableTablesCount == 0) return false;

        var overlappingCount = await DbSet.IgnoreQueryFilters().AsNoTracking()
            .CountAsync(r => r.RestaurantId == restaurantId
                && r.Status != ReservationStatus.Cancelled
                && r.Status != ReservationStatus.NoShow
                && r.ReservedAt < to
                && r.ReservedAt.AddMinutes(r.DurationMinutes) > from, cancellationToken);

        return overlappingCount < capableTablesCount;
    }
}
