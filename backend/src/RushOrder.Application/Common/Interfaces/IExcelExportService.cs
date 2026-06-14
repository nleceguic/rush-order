using RushOrder.Application.Analytics.DTOs;

namespace RushOrder.Application.Common.Interfaces;

public interface IExcelExportService
{
    byte[] GenerateSalesExcel(SalesDto sales, DateTimeOffset from, DateTimeOffset to);
}
