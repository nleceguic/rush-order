using RushOrder.Desktop.Models;

namespace RushOrder.Desktop.Models;

public enum KitchenStation
{
    All         = 0,
    HotKitchen  = 1,
    ColdKitchen = 2,
    Grill       = 3,
    Dessert     = 4,
    Bar         = 5,
}

public static class KitchenStationHelper
{
    public static string DisplayName(this KitchenStation s) => s switch
    {
        KitchenStation.All         => "Todos",
        KitchenStation.HotKitchen  => "Cocina caliente",
        KitchenStation.ColdKitchen => "Cocina fría",
        KitchenStation.Grill       => "Plancha",
        KitchenStation.Dessert     => "Postre",
        KitchenStation.Bar         => "Barra",
        _                          => s.ToString(),
    };

    public static KitchenStation Detect(string productName)
    {
        var n = productName.ToLowerInvariant();
        if (n.Contains("postre") || n.Contains("tarta") || n.Contains("helado") ||
            n.Contains("flan")   || n.Contains("tiramisú") || n.Contains("tiramisu") ||
            n.Contains("mousse") || n.Contains("brownie"))
            return KitchenStation.Dessert;
        if (n.Contains("cerveza") || n.Contains("vino") || n.Contains("agua") ||
            n.Contains("refresco") || n.Contains("café") || n.Contains("cafe") ||
            n.Contains("zumo")     || n.Contains("licor") || n.Contains("copa"))
            return KitchenStation.Bar;
        if (n.Contains("ensalada") || n.Contains("gazpacho") || n.Contains("carpaccio") ||
            n.Contains("tartar")   || n.Contains("ceviche")  || n.Contains("frío") || n.Contains("fría"))
            return KitchenStation.ColdKitchen;
        if (n.Contains("chuletón") || n.Contains("entrecot") || n.Contains("hamburguesa") ||
            n.Contains("filete")   || n.Contains("plancha")  || n.Contains("brasa") ||
            n.Contains("lomo")     || n.Contains("solomillo"))
            return KitchenStation.Grill;
        return KitchenStation.HotKitchen;
    }
}

public sealed record KitchenItemDto(
    Guid   ItemId,
    Guid   ProductId,
    string ProductName,
    int    Quantity,
    IReadOnlyList<string> Allergens,
    IReadOnlyList<string> Modifiers,
    string? Notes,
    KitchenStation Station);

public sealed record KitchenOrderDto(
    Guid   OrderId,
    string OrderNumber,
    string TableName,
    int    GuestCount,
    DateTimeOffset PlacedAt,
    OrderStatus    Status,
    bool           IsUrgent,
    IReadOnlyList<KitchenItemDto> Items)
{
    public TimeSpan Age          => DateTimeOffset.Now - PlacedAt;
    public bool     IsReadyAndOld => Status == OrderStatus.Ready && Age.TotalMinutes > 3;
}

public sealed record KitchenShiftStats(
    int      CompletedOrders,
    TimeSpan AvgTime,
    int      PendingOrders,
    DateTimeOffset ShiftStart);
