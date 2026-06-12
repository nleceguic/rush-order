namespace RushOrder.Domain.ValueObjects;

public sealed record TenantSettings
{
    public string DefaultLanguage { get; init; } = "es";
    public IReadOnlyList<string> SupportedLanguages { get; init; } = ["es", "en"];
    public IReadOnlyList<string> PaymentMethods { get; init; } = ["card", "cash"];
    public string BrandColor { get; init; } = "#FF6B35";
    public string LogoUrl { get; init; } = string.Empty;
    public bool ModuleReservations { get; init; } = true;
    public bool ModuleDelivery { get; init; } = false;
    public bool ModuleLoyalty { get; init; } = true;
    public bool ModuleInventory { get; init; } = false;

    public static TenantSettings Default => new();
}
