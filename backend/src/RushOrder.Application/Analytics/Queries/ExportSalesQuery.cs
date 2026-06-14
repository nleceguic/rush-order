using MediatR;
using RushOrder.Application.Common.Interfaces;

namespace RushOrder.Application.Analytics.Queries;

public record ExportSalesQuery(
    Guid RestaurantId,
    DateTimeOffset From,
    DateTimeOffset To) : IQuery<byte[]>;

public sealed class ExportSalesQueryHandler : IRequestHandler<ExportSalesQuery, byte[]>
{
    private readonly IAnalyticsRepository _analytics;
    private readonly IExcelExportService _excel;

    public ExportSalesQueryHandler(IAnalyticsRepository analytics, IExcelExportService excel)
    {
        _analytics = analytics;
        _excel = excel;
    }

    public async Task<byte[]> Handle(ExportSalesQuery request, CancellationToken cancellationToken)
    {
        var sales = await _analytics.GetSalesAsync(
            request.RestaurantId, request.From, request.To, "day", cancellationToken);

        return _excel.GenerateSalesExcel(sales, request.From, request.To);
    }
}
