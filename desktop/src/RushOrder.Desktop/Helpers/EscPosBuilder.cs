using System.Text;
using RushOrder.Desktop.Models;

namespace RushOrder.Desktop.Helpers;

public sealed class EscPosBuilder
{
    private readonly List<byte> _bytes = [];
    private static readonly Encoding _enc = Encoding.GetEncoding(850); // Latin-1 with euros

    public EscPosBuilder Initialize()
    {
        _bytes.AddRange([0x1B, 0x40]);   // ESC @ — reset printer
        _bytes.AddRange([0x1B, 0x74, 0x02]); // ESC t 2 — code page 850
        return this;
    }

    public EscPosBuilder Bold(bool on)
    {
        _bytes.AddRange([0x1B, 0x45, on ? (byte)1 : (byte)0]);
        return this;
    }

    public EscPosBuilder Align(Alignment a)
    {
        _bytes.AddRange([0x1B, 0x61, (byte)a]);
        return this;
    }

    public EscPosBuilder DoubleHeight(bool on)
    {
        _bytes.AddRange([0x1B, 0x21, on ? (byte)0x10 : (byte)0x00]);
        return this;
    }

    public EscPosBuilder Text(string text)
    {
        _bytes.AddRange(_enc.GetBytes(text));
        return this;
    }

    public EscPosBuilder Line(string text = "")
    {
        _bytes.AddRange(_enc.GetBytes(text));
        _bytes.Add(0x0A);
        return this;
    }

    public EscPosBuilder Separator(char ch = '-', int width = 32)
    {
        _bytes.AddRange(_enc.GetBytes(new string(ch, width)));
        _bytes.Add(0x0A);
        return this;
    }

    public EscPosBuilder Cut()
    {
        _bytes.AddRange([0x0A, 0x0A, 0x0A]);
        _bytes.AddRange([0x1D, 0x56, 0x42, 0x00]); // GS V B 0 — full cut
        return this;
    }

    public EscPosBuilder LineFeed(int n = 1)
    {
        for (int i = 0; i < n; i++) _bytes.Add(0x0A);
        return this;
    }

    // GS ! — double-width (width=2,height=1) → 0x10; double-height → 0x01; both → 0x11
    public EscPosBuilder FontSize(int width = 1, int height = 1)
    {
        byte n = (byte)(((Math.Clamp(width,  1, 8) - 1) << 4)
                       | (Math.Clamp(height, 1, 8) - 1));
        _bytes.AddRange([0x1D, 0x21, n]);
        return this;
    }

    // Full ESC/POS QR Code sequence (model 2, error correction M)
    public EscPosBuilder QRCode(string data, int moduleSize = 4)
    {
        var dataBytes = _enc.GetBytes(data);
        int storeLen  = dataBytes.Length + 3; // fn=P, m=0 + data bytes counted as pL,pH + 1
        byte pL = (byte)(storeLen & 0xFF);
        byte pH = (byte)(storeLen >> 8);

        _bytes.AddRange([0x1D, 0x28, 0x6B, 0x04, 0x00, 0x31, 0x41, 0x32, 0x00]); // model 2
        _bytes.AddRange([0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x43, (byte)Math.Clamp(moduleSize, 1, 8)]); // size
        _bytes.AddRange([0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x45, 0x31]);        // error M
        _bytes.AddRange([0x1D, 0x28, 0x6B, pL,   pH,   0x31, 0x50, 0x30]);        // store data
        _bytes.AddRange(dataBytes);
        _bytes.AddRange([0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x51, 0x30]);        // print
        return this;
    }

    public byte[] Build() => [.. _bytes];

    // ── Full-featured ticket (from PrintService) ──────────────────────────

    public static byte[] BuildFullTicket(OrderDto order, PaymentRequest payment, PrinterConfig cfg)
    {
        var method = payment.Method switch
        {
            "Cash" => "Efectivo",
            "Card" => "Tarjeta",
            "Qr"   => "QR",
            _      => payment.Method,
        };

        var b = new EscPosBuilder().Initialize()
            .Align(Alignment.Center)
            .Bold(true).FontSize(2, 1)
            .Line(cfg.RestaurantName)
            .FontSize(1, 1).Bold(false);

        if (!string.IsNullOrEmpty(cfg.RestaurantAddress)) b.Line(cfg.RestaurantAddress);
        if (!string.IsNullOrEmpty(cfg.RestaurantCif))     b.Line($"CIF: {cfg.RestaurantCif}");

        b.Separator()
         .Align(Alignment.Left)
         .Bold(true).Line($"Pedido: #{order.OrderNumber}").Bold(false)
         .Line($"Mesa: {order.TableName}  ·  {order.GuestCount} pax")
         .Line($"Fecha: {DateTime.Now:dd/MM/yyyy HH:mm}")
         .Line($"Camarero: {order.WaiterName ?? "—"}")
         .Separator();

        foreach (var item in order.Items)
        {
            var qty   = $"{item.Quantity}x";
            var name  = item.ProductName.Length > 20 ? item.ProductName[..20] : item.ProductName;
            var price = $"€{item.LineTotal:N2}";
            var pad   = 32 - qty.Length - 1 - name.Length - price.Length;
            b.Line($"{qty} {name}{new string(' ', Math.Max(1, pad))}{price}");
            if (!string.IsNullOrEmpty(item.Notes))
                b.Line($"   * {item.Notes}");
        }

        // IVA breakdown — all food & drinks at 10% (Spain restauración)
        b.Separator()
         .Align(Alignment.Right)
         .Line($"Base (10%): € {order.Subtotal:N2}")
         .Line($"IVA (10%):  € {order.Tax:N2}")
         .Bold(true).Line($"TOTAL:      € {order.Total:N2}").Bold(false)
         .Line($"Pago: {method}");

        if (payment.Method == "Cash")
        {
            b.Line($"Entregado: € {payment.AmountTendered:N2}");
            b.Line($"Cambio:    € {Math.Max(0, payment.AmountTendered - order.Total):N2}");
        }
        if (payment.SplitParts > 1)
            b.Line($"Por persona: € {order.Total / payment.SplitParts:N2}");

        b.Separator()
         .Align(Alignment.Center)
         .Line("Recibo digital:")
         .QRCode($"{cfg.ReceiptBaseUrl}{order.OrderNumber}", 4)
         .LineFeed(1)
         .Line(cfg.ThanksMessage)
         .Cut();

        return b.Build();
    }

    public static byte[] BuildFullKitchenComanda(OrderDto order, bool isPreOrder = false)
    {
        var b = new EscPosBuilder().Initialize().Align(Alignment.Center);

        if (isPreOrder)
            b.Bold(true).Line("*** NUEVO QR ***").Bold(false);

        b.FontSize(2, 2).Bold(true).Line($"#{order.OrderNumber}").Bold(false).FontSize(1, 1)
         .FontSize(2, 1).Line(order.TableName).FontSize(1, 1)
         .Line(DateTime.Now.ToString("HH:mm"))
         .Separator('=', 32)
         .Align(Alignment.Left);

        foreach (var item in order.Items)
        {
            b.Bold(true).Line($"{item.Quantity}x  {item.ProductName}").Bold(false);
            if (!string.IsNullOrEmpty(item.Notes))
                b.Line($"   >> {item.Notes}");
            if (item.Allergens.Count > 0)
                b.Line($"   * ALERG: {string.Join(", ", item.Allergens)}");
            b.LineFeed(1);
        }

        if (!string.IsNullOrEmpty(order.Notes))
            b.Separator().Bold(true).Line($"NOTA: {order.Notes}").Bold(false);

        b.Separator('=', 32).LineFeed(3).Cut();
        return b.Build();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    public static byte[] BuildComanda(OrderDto order)
    {
        var b = new EscPosBuilder()
            .Initialize()
            .Align(Alignment.Center)
            .Bold(true)
            .DoubleHeight(true)
            .Line("RushOrder")
            .DoubleHeight(false)
            .Bold(false)
            .Line(DateTime.Now.ToString("dd/MM/yyyy HH:mm"))
            .Separator()
            .Align(Alignment.Left)
            .Bold(true)
            .Line($"Pedido: #{order.OrderNumber}")
            .Bold(false)
            .Line($"Mesa: {order.TableName}  ({order.GuestCount} pax)")
            .Line($"Camarero: {order.WaiterName ?? "—"}")
            .Separator();

        foreach (var item in order.Items)
        {
            var qty     = $"{item.Quantity}x";
            var name    = item.ProductName.Length > 22 ? item.ProductName[..22] : item.ProductName;
            var price   = $"€{item.LineTotal:N2}";
            var padding = 32 - qty.Length - name.Length - price.Length;
            b.Line($"{qty} {name}{new string(' ', Math.Max(1, padding))}{price}");

            if (!string.IsNullOrEmpty(item.Notes))
                b.Line($"   >> {item.Notes}");
        }

        if (!string.IsNullOrEmpty(order.Notes))
            b.Separator().Line($"Nota: {order.Notes}");

        b.Separator()
         .Align(Alignment.Right)
         .Bold(true)
         .Line($"TOTAL: € {order.Total:N2}")
         .Bold(false)
         .Align(Alignment.Center)
         .Line()
         .Line("Gracias por su visita")
         .Cut();

        return b.Build();
    }

    public static byte[] BuildTicket(OrderDto order, PaymentRequest payment)
    {
        var method = payment.Method switch
        {
            "Cash" => "Efectivo",
            "Card" => "Tarjeta",
            "Qr"   => "QR",
            _      => payment.Method,
        };

        var b = new EscPosBuilder()
            .Initialize()
            .Align(Alignment.Center)
            .Bold(true)
            .DoubleHeight(true)
            .Line("TICKET")
            .DoubleHeight(false)
            .Bold(false)
            .Line(DateTime.Now.ToString("dd/MM/yyyy HH:mm"))
            .Line($"#{order.OrderNumber}  Mesa {order.TableName}")
            .Separator();

        foreach (var item in order.Items)
            b.Line($"{item.Quantity}x {item.ProductName,-22} € {item.LineTotal:N2}");

        b.Separator()
         .Align(Alignment.Right)
         .Line($"Subtotal:   € {order.Subtotal:N2}")
         .Line($"IVA (10%):  € {order.Tax:N2}")
         .Bold(true).Line($"TOTAL:      € {order.Total:N2}").Bold(false)
         .Line($"Método:     {method}");

        if (payment.Method == "Cash")
        {
            b.Line($"Recibido:   € {payment.AmountTendered:N2}");
            b.Line($"Cambio:     € {Math.Max(0, payment.AmountTendered - order.Total):N2}");
        }

        if (payment.SplitParts > 1)
            b.Line($"Por persona: € {order.Total / payment.SplitParts:N2}");

        b.Separator()
         .Align(Alignment.Center)
         .Line("Gracias por su visita")
         .Line("www.rushorder.app")
         .Cut();

        return b.Build();
    }

    public enum Alignment : byte { Left = 0, Center = 1, Right = 2 }
}
