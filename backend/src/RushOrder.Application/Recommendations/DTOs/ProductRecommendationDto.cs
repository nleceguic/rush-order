namespace RushOrder.Application.Recommendations.DTOs;

// Reason is always one of the four fixed strings the PWA switches on:
// "Muy pedido hoy" | "Perfecto con tu selección" | "El chef recomienda" | "Los clientes también piden"
// — see RecommendationReasons in RushOrder.Infrastructure.Services.RecommendationService.
public sealed record ProductRecommendationDto(
    Guid ProductId,
    string Name,
    string? ImageUrl,
    decimal Price,
    string Currency,
    string Reason,
    decimal ConfidenceScore);
