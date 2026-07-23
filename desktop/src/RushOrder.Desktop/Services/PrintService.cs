using System.Drawing.Printing;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RushOrder.Desktop.Helpers;
using RushOrder.Desktop.Models;

namespace RushOrder.Desktop.Services;

public sealed class PrintService
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RushOrder", "printers.json");

    private readonly ILogger<PrintService> _logger;

    public PrinterConfig Config { get; private set; } = new();

    public PrintService(ILogger<PrintService> logger)
    {
        _logger = logger;
        LoadConfig();
    }

    public void LoadConfig()
    {
        try
        {
            if (File.Exists(ConfigPath))
                Config = JsonConvert.DeserializeObject<PrinterConfig>(
                    File.ReadAllText(ConfigPath)) ?? new PrinterConfig();
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not load printer config"); }
    }

    public void SaveConfig(PrinterConfig cfg)
    {
        Config = cfg;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(cfg, Formatting.Indented));
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not save printer config"); }
    }

    // ── ESC/POS printing ─────────────────────────────────────────────────────

    public bool PrintCustomerTicket(OrderDto order, PaymentRequest payment)
    {
        if (string.IsNullOrEmpty(Config.CajaPrinterName))
        {
            _logger.LogWarning("No cash (caja) printer configured");
            return false;
        }
        var bytes = EscPosBuilder.BuildFullTicket(order, payment, Config);
        return RawPrinterHelper.SendToPrinter(Config.CajaPrinterName, bytes,
            $"Ticket #{order.OrderNumber}");
    }

    public bool PrintKitchenComanda(OrderDto order, bool isPreOrder = false)
    {
        if (string.IsNullOrEmpty(Config.CocinaPrinterName))
        {
            _logger.LogWarning("No kitchen (cocina) printer configured");
            return false;
        }
        var bytes = EscPosBuilder.BuildFullKitchenComanda(order, isPreOrder);
        return RawPrinterHelper.SendToPrinter(Config.CocinaPrinterName, bytes,
            $"Comanda #{order.OrderNumber}");
    }

    // ── GDI test page ─────────────────────────────────────────────────────────

    public bool PrintTestPage(string printerName)
    {
        try
        {
            var pd = new PrintDocument
            {
                PrinterSettings = { PrinterName = printerName }
            };
            pd.PrintPage += (_, e) =>
            {
                var g = e.Graphics!;
                using var titleFont = new Font("Arial", 14, FontStyle.Bold);
                using var bodyFont  = new Font("Arial", 10);
                using var grayBrush = new SolidBrush(Color.Gray);
                using var pen       = new Pen(Color.Black, 1);

                g.DrawString("Rush Order — Página de prueba", titleFont, Brushes.Black, 20, 20);
                g.DrawLine(pen, 20, 46, 400, 46);
                g.DrawString($"Impresora: {printerName}", bodyFont, Brushes.Black, 20, 54);
                g.DrawString(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"), bodyFont, grayBrush, 20, 72);
                g.DrawString("La impresora está configurada correctamente.", bodyFont, Brushes.Black, 20, 90);
                g.DrawLine(pen, 20, 112, 400, 112);
                g.DrawString("www.rushorder.app", bodyFont, grayBrush, 20, 120);
            };
            pd.Print();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Test page failed on {Printer}", printerName);
            return false;
        }
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    public static string[] GetAvailablePrinters()
    {
        var list = new List<string>();
        foreach (string name in PrinterSettings.InstalledPrinters)
            list.Add(name);
        return [.. list];
    }
}
