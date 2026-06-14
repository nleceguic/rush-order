using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RushOrder.Application.Common.Interfaces;

namespace RushOrder.Infrastructure.Services;

public sealed class QuestPdfReceiptService : IPdfReceiptService
{
    public byte[] GenerateReceipt(ReceiptData data)
        => Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Content().Column(col =>
                {
                    // Header
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(inner =>
                        {
                            inner.Item().Text(data.RestaurantName).Bold().FontSize(18);
                            inner.Item().Text(data.RestaurantAddress).FontColor(Colors.Grey.Medium);
                            inner.Item().Text(data.RestaurantPhone).FontColor(Colors.Grey.Medium);
                            inner.Item().Text(data.RestaurantEmail).FontColor(Colors.Grey.Medium);
                        });
                        row.ConstantItem(120).AlignRight().Column(inner =>
                        {
                            inner.Item().Text("RECIBO").Bold().FontSize(16);
                            inner.Item().Text($"#{data.OrderNumber}").FontSize(12);
                            inner.Item().Text(data.PaidAt.ToString("dd/MM/yyyy HH:mm")).FontColor(Colors.Grey.Medium);
                        });
                    });

                    col.Item().PaddingVertical(16).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    // Items table
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(4);
                            cols.RelativeColumn(1);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("Producto").Bold();
                            header.Cell().AlignCenter().Text("Cant.").Bold();
                            header.Cell().AlignRight().Text("Precio unit.").Bold();
                            header.Cell().AlignRight().Text("Total").Bold();

                            header.Cell().ColumnSpan(4).PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                        });

                        foreach (var item in data.Items)
                        {
                            table.Cell().PaddingVertical(4).Text(item.Name);
                            table.Cell().PaddingVertical(4).AlignCenter().Text(item.Quantity.ToString());
                            table.Cell().PaddingVertical(4).AlignRight().Text($"{item.UnitPrice:F2} {item.Currency}");
                            table.Cell().PaddingVertical(4).AlignRight().Text($"{item.LineTotal:F2} {item.Currency}");
                        }
                    });

                    col.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    // Totals
                    col.Item().AlignRight().Column(totals =>
                    {
                        TotalRow(totals, "Subtotal:", data.Subtotal, data.Currency, false);

                        if (data.DiscountAmount > 0)
                            TotalRow(totals, "Descuento:", -data.DiscountAmount, data.Currency, false);

                        TotalRow(totals, $"IVA ({data.TaxRate * 100:F0}%):", data.TaxAmount, data.Currency, false);

                        if (data.TipAmount > 0)
                            TotalRow(totals, "Propina:", data.TipAmount, data.Currency, false);

                        totals.Item().PaddingVertical(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                        TotalRow(totals, "TOTAL:", data.Total, data.Currency, true);
                    });

                    col.Item().PaddingTop(24).AlignCenter()
                        .Text($"ID de pago: {data.PaymentId}")
                        .FontSize(9).FontColor(Colors.Grey.Medium);
                });
            });
        }).GeneratePdf();

    private static void TotalRow(ColumnDescriptor col, string label, decimal amount, string currency, bool bold)
    {
        col.Item().Row(r =>
        {
            if (bold)
            {
                r.ConstantItem(160).Text(label).Bold();
                r.ConstantItem(100).AlignRight().Text($"{amount:F2} {currency}").Bold();
            }
            else
            {
                r.ConstantItem(160).Text(label);
                r.ConstantItem(100).AlignRight().Text($"{amount:F2} {currency}");
            }
        });
    }
}
