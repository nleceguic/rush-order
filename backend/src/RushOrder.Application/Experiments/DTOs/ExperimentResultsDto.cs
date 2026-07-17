namespace RushOrder.Application.Experiments.DTOs;

public sealed record ExperimentVariantStats(
    string Variant,
    int Exposures,
    int SuggestionAdds,
    int OrdersCompleted,
    decimal AvgCartTotal)
{
    // % of exposed sessions that added at least one suggested product — the
    // primary metric for "Recomendaciones en el carrito" (spec: "% que añade
    // al menos 1 producto sugerido").
    public decimal ConversionRate => Exposures == 0 ? 0m : Math.Round((decimal)SuggestionAdds / Exposures * 100m, 1);
}

public sealed record ExperimentResultsDto(string ExperimentKey, IReadOnlyList<ExperimentVariantStats> Variants);
