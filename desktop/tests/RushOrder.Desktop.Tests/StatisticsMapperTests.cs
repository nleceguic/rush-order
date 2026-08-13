using FluentAssertions;
using RushOrder.Desktop.Services;

namespace RushOrder.Desktop.Tests;

public class StatisticsMapperTests
{
    private static readonly DateOnly From = new(2026, 8, 1);
    private static readonly DateOnly To   = new(2026, 8, 7);

    [Fact]
    public void Map_GroupsSalesSeriesByHour_SummingRevenueAndSortingAscending()
    {
        var sales = new BackendSalesDto(
            Series:
            [
                new BackendSalesSeriesPoint(new DateTimeOffset(2026, 8, 1, 14, 0, 0, TimeSpan.Zero), 30m, 2, 4),
                new BackendSalesSeriesPoint(new DateTimeOffset(2026, 8, 2, 9, 0, 0, TimeSpan.Zero), 10m, 1, 1),
                new BackendSalesSeriesPoint(new DateTimeOffset(2026, 8, 3, 14, 0, 0, TimeSpan.Zero), 20m, 1, 2),
            ],
            Totals: new BackendSalesTotals(60m, 4, 15m, null, null));

        var result = StatisticsMapper.Map(From, To, sales, [], []);

        result.HourlyRevenue.Should().Equal(
            new RushOrder.Desktop.Models.HourlyRevenuePoint(9, 10m),
            new RushOrder.Desktop.Models.HourlyRevenuePoint(14, 50m));
        result.TotalRevenue.Should().Be(60m);
        result.TotalOrders.Should().Be(4);
    }

    [Fact]
    public void Map_TakesTop10ProductsByRevenueDescending()
    {
        var sales = new BackendSalesDto([], new BackendSalesTotals(0m, 0, 0m, null, null));
        var products = Enumerable.Range(1, 12)
            .Select(i => new BackendProductPerformanceDto(
                Guid.NewGuid(), $"Producto {i}", "Cat", i, i * 10m, null, "flat", null))
            .ToList();

        var result = StatisticsMapper.Map(From, To, sales, products, []);

        result.TopProducts.Should().HaveCount(10);
        result.TopProducts.Select(p => p.Name).First().Should().Be("Producto 12");
        result.TopProducts.Select(p => p.Name).Last().Should().Be("Producto 3");
    }

    [Fact]
    public void Map_MapsWaiters_WithAvgMinutesAlwaysZero()
    {
        var sales = new BackendSalesDto([], new BackendSalesTotals(0m, 0, 0m, null, null));
        var waiters = new List<BackendWaiterPerformanceDto>
        {
            new(Guid.NewGuid(), "Ana García", 12, 4.5m, 320.50m, 26.70m),
        };

        var result = StatisticsMapper.Map(From, To, sales, [], waiters);

        result.WaiterStats.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new RushOrder.Desktop.Models.WaiterStatsRow("Ana García", 12, 320.50m, 0, 26.70m));
    }

    [Fact]
    public void Map_AlwaysReturnsEmptyPaymentMethods_NoBackendEndpointExistsYet()
    {
        var sales = new BackendSalesDto([], new BackendSalesTotals(0m, 0, 0m, null, null));

        var result = StatisticsMapper.Map(From, To, sales, [], []);

        result.PaymentMethods.Should().BeEmpty();
    }

    [Fact]
    public void Map_WithEmptySeriesProductsAndWaiters_ReturnsEmptyListsNotAnException()
    {
        var sales = new BackendSalesDto([], new BackendSalesTotals(0m, 0, 0m, null, null));

        var result = StatisticsMapper.Map(From, To, sales, [], []);

        result.HourlyRevenue.Should().BeEmpty();
        result.TopProducts.Should().BeEmpty();
        result.WaiterStats.Should().BeEmpty();
        result.TotalOrders.Should().Be(0);
        result.From.Should().Be(From);
        result.To.Should().Be(To);
    }
}
