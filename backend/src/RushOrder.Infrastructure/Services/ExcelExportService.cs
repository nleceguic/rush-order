using ClosedXML.Excel;
using RushOrder.Application.Analytics.DTOs;
using RushOrder.Application.Common.Interfaces;

namespace RushOrder.Infrastructure.Services;

public sealed class ExcelExportService : IExcelExportService
{
    public byte[] GenerateSalesExcel(SalesDto sales, DateTimeOffset from, DateTimeOffset to)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Ventas");

        // ── Header ────────────────────────────────────────────────────────────
        sheet.Cell(1, 1).Value = "Rush Order — Informe de Ventas";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;

        sheet.Cell(2, 1).Value = "Período:";
        sheet.Cell(2, 2).Value = $"{from:dd/MM/yyyy} – {to:dd/MM/yyyy}";

        // ── Resumen ───────────────────────────────────────────────────────────
        sheet.Cell(4, 1).Value = "RESUMEN";
        sheet.Cell(4, 1).Style.Font.Bold = true;
        sheet.Cell(4, 1).Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
        sheet.Range(4, 1, 4, 2).Merge();

        var summaryRows = new[]
        {
            ("Ingresos totales",  (object)sales.Totals.Revenue),
            ("Total pedidos",     (object)sales.Totals.Orders),
            ("Ticket medio",      (object)sales.Totals.AvgTicket),
            ("Mejor día",         (object)(sales.Totals.BestDay?.ToString("dd/MM/yyyy") ?? "—")),
            ("Peor día",          (object)(sales.Totals.WorstDay?.ToString("dd/MM/yyyy") ?? "—"))
        };

        for (var i = 0; i < summaryRows.Length; i++)
        {
            sheet.Cell(5 + i, 1).Value = summaryRows[i].Item1;
            sheet.Cell(5 + i, 2).Value = summaryRows[i].Item2.ToString();
        }

        // Format currency cells
        sheet.Cell(5, 2).Style.NumberFormat.Format = "#,##0.00 €";
        sheet.Cell(7, 2).Style.NumberFormat.Format = "#,##0.00 €";

        // ── Series table ─────────────────────────────────────────────────────
        const int headerRow = 12;

        sheet.Cell(headerRow, 1).Value = "Fecha";
        sheet.Cell(headerRow, 2).Value = "Ingresos (€)";
        sheet.Cell(headerRow, 3).Value = "Pedidos";
        sheet.Cell(headerRow, 4).Value = "Comensales";

        var headerRange = sheet.Range(headerRow, 1, headerRow, 4);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        var row = headerRow + 1;
        foreach (var point in sales.Series)
        {
            sheet.Cell(row, 1).Value = point.Date.ToString("dd/MM/yyyy HH:mm");
            sheet.Cell(row, 2).Value = point.Revenue;
            sheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(row, 3).Value = point.Orders;
            sheet.Cell(row, 4).Value = point.Covers;
            row++;
        }

        // Totals row
        if (sales.Series.Count > 0)
        {
            sheet.Cell(row, 1).Value = "TOTAL";
            sheet.Cell(row, 1).Style.Font.Bold = true;
            sheet.Cell(row, 2).FormulaA1 = $"SUM(B{headerRow + 1}:B{row - 1})";
            sheet.Cell(row, 2).Style.Font.Bold = true;
            sheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(row, 3).FormulaA1 = $"SUM(C{headerRow + 1}:C{row - 1})";
            sheet.Cell(row, 3).Style.Font.Bold = true;
            sheet.Cell(row, 4).FormulaA1 = $"SUM(D{headerRow + 1}:D{row - 1})";
            sheet.Cell(row, 4).Style.Font.Bold = true;
        }

        sheet.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }
}
