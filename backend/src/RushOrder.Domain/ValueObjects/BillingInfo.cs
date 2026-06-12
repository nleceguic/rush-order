namespace RushOrder.Domain.ValueObjects;

public sealed record BillingInfo
{
    public string CompanyName { get; init; } = string.Empty;
    public string TaxId { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string Country { get; init; } = "ES";
    public string InvoiceEmail { get; init; } = string.Empty;

    public static BillingInfo Empty => new();
}
