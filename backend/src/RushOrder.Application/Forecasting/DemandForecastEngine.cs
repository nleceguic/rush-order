using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;
using RushOrder.Domain.Enums;

namespace RushOrder.Application.Forecasting;

// Moving Average + Day-of-Week adjustment (no ML libraries, by design):
//
//   base_forecast(product, hour) = avg of the last (up to 4) matching weeks'
//                                   same weekday/hour quantity
//   seasonality_factor           = last_week_qty / base_forecast, smoothed
//                                   toward 1.0 via a single-step exponential
//                                   smoothing (alpha=0.3)
//   forecast = base_forecast * seasonality_factor * day_multiplier
//              (* 1.5 again if forecast_date is a known public holiday)
//
// day_multiplier only had Mon/Tue/Fri/Sat/Sun given explicitly in the spec;
// Wed/Thu are a linear interpolation between Tue and Fri (0.90 / 1.00).
public sealed class DemandForecastEngine
{
    private const decimal SmoothingAlpha = 0.3m;
    private const decimal HolidayMultiplier = 1.5m;
    private const int MaxHistoryWeeks = 4;

    private static readonly IReadOnlyDictionary<DayOfWeek, decimal> DayMultipliers = new Dictionary<DayOfWeek, decimal>
    {
        [DayOfWeek.Monday] = 0.80m,
        [DayOfWeek.Tuesday] = 0.85m,
        [DayOfWeek.Wednesday] = 0.90m,
        [DayOfWeek.Thursday] = 1.00m,
        [DayOfWeek.Friday] = 1.20m,
        [DayOfWeek.Saturday] = 1.40m,
        [DayOfWeek.Sunday] = 1.30m,
    };

    private readonly IHolidayProvider _holidayProvider;

    public DemandForecastEngine(IHolidayProvider holidayProvider)
    {
        _holidayProvider = holidayProvider;
    }

    public async Task<IReadOnlyList<DemandForecast>> BuildForecastsAsync(
        Guid tenantId,
        Guid restaurantId,
        IReadOnlyList<ForecastProductRow> products,
        IReadOnlyList<HistoricalSaleRow> history,
        DateOnly startDate,
        int days,
        TimeOnly openingTime,
        TimeOnly closingTime,
        CancellationToken cancellationToken = default)
    {
        var years = Enumerable.Range(0, days).Select(d => startDate.AddDays(d).Year).Distinct();
        var holidays = new HashSet<DateOnly>();
        foreach (var year in years)
            holidays.UnionWith(await _holidayProvider.GetHolidaysAsync(year, cancellationToken));

        // .NET's DayOfWeek (Sunday=0..Saturday=6) matches Postgres EXTRACT(DOW),
        // so HistoricalSaleRow.DayOfWeek can be compared against it directly.
        var historyByKey = history
            .GroupBy(h => (h.ProductId, h.DayOfWeek, h.Hour))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(h => h.LocalDate).Take(MaxHistoryWeeks).ToList());

        var operatingHours = BuildOperatingHours(openingTime, closingTime);
        var results = new List<DemandForecast>(products.Count * operatingHours.Count * days);

        for (var dayOffset = 0; dayOffset < days; dayOffset++)
        {
            var forecastDate = startDate.AddDays(dayOffset);
            var dayOfWeek = forecastDate.DayOfWeek;
            var multiplier = DayMultipliers[dayOfWeek] * (holidays.Contains(forecastDate) ? HolidayMultiplier : 1m);
            var pgDow = (int)dayOfWeek;

            foreach (var product in products)
            {
                foreach (var hour in operatingHours)
                {
                    var weeks = historyByKey.GetValueOrDefault((product.ProductId, pgDow, hour)) ?? [];

                    if (weeks.Count == 0)
                    {
                        results.Add(DemandForecast.Create(
                            tenantId, restaurantId, product.ProductId, forecastDate, hour,
                            predictedQuantity: 0, ForecastConfidence.Low));
                        continue;
                    }

                    var baseForecast = weeks.Average(w => (decimal)w.Quantity);
                    var lastWeekQty = (decimal)weeks[0].Quantity;
                    var rawSeasonality = baseForecast == 0 ? 1m : lastWeekQty / baseForecast;
                    var smoothedSeasonality = SmoothingAlpha * rawSeasonality + (1 - SmoothingAlpha);

                    var predictedQuantity = baseForecast * smoothedSeasonality * multiplier;
                    var confidence = weeks.Count >= 4 ? ForecastConfidence.High
                        : weeks.Count >= 2 ? ForecastConfidence.Medium
                        : ForecastConfidence.Low;

                    results.Add(DemandForecast.Create(
                        tenantId, restaurantId, product.ProductId, forecastDate, hour,
                        predictedQuantity, confidence));
                }
            }
        }

        return results;
    }

    private static List<int> BuildOperatingHours(TimeOnly opening, TimeOnly closing)
    {
        var hours = new List<int>();
        if (closing.Hour >= opening.Hour)
        {
            for (var h = opening.Hour; h <= closing.Hour; h++) hours.Add(h);
        }
        else
        {
            // Crosses midnight (e.g. 18:00 -> 02:00)
            for (var h = opening.Hour; h <= 23; h++) hours.Add(h);
            for (var h = 0; h <= closing.Hour; h++) hours.Add(h);
        }
        return hours;
    }
}
