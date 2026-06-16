namespace RushOrder.Desktop.Models;

public sealed class PrinterConfig
{
    public string? CajaPrinterName   { get; set; }
    public string? CocinaPrinterName { get; set; }
    public string? BarraPrinterName  { get; set; }
    public bool    EscPosCaja        { get; set; } = true;
    public bool    EscPosCocina      { get; set; } = true;
    public string  RestaurantName    { get; set; } = "Mi Restaurante";
    public string  RestaurantAddress { get; set; } = "";
    public string  RestaurantCif     { get; set; } = "";
    public string  ThanksMessage     { get; set; } = "¡Gracias por su visita!";
    public string  ReceiptBaseUrl    { get; set; } = "https://rushorder.app/r/";
}
