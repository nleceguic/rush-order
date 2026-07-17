namespace RushOrder.Application.Common.Interfaces;

public interface IHolidayProvider
{
    // Spain national public holidays for the given year. Regional (e.g.
    // Catalonia-specific) holidays aren't included — see
    // NagerDateHolidayProvider for why.
    Task<IReadOnlySet<DateOnly>> GetHolidaysAsync(int year, CancellationToken cancellationToken = default);
}
