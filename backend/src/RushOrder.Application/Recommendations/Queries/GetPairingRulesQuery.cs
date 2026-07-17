using MediatR;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Recommendations.DTOs;

namespace RushOrder.Application.Recommendations.Queries;

public record GetPairingRulesQuery(Guid RestaurantId) : IQuery<IReadOnlyList<PairingRuleDto>>;

public sealed class GetPairingRulesQueryHandler : IRequestHandler<GetPairingRulesQuery, IReadOnlyList<PairingRuleDto>>
{
    private readonly IPairingRuleRepository _pairingRules;
    private readonly IProductRepository _products;

    public GetPairingRulesQueryHandler(IPairingRuleRepository pairingRules, IProductRepository products)
    {
        _pairingRules = pairingRules;
        _products = products;
    }

    public async Task<IReadOnlyList<PairingRuleDto>> Handle(GetPairingRulesQuery request, CancellationToken cancellationToken)
    {
        var rules = await _pairingRules.GetByRestaurantAsync(request.RestaurantId, cancellationToken);
        if (rules.Count == 0) return [];

        // Pairing rules are a small, manually curated list (a handful to a few
        // dozen per restaurant) — resolving product names one at a time here
        // is fine and avoids adding a bulk-lookup method just for this admin screen.
        var productNames = new Dictionary<Guid, string>();
        foreach (var productId in rules.SelectMany(r => new[] { r.SourceProductId, r.TargetProductId }).Distinct())
        {
            var product = await _products.GetByIdAsync(productId, cancellationToken);
            if (product is not null) productNames[productId] = product.Name;
        }

        return rules
            .Select(r => new PairingRuleDto(
                r.Id,
                r.SourceProductId, productNames.GetValueOrDefault(r.SourceProductId, "(producto eliminado)"),
                r.TargetProductId, productNames.GetValueOrDefault(r.TargetProductId, "(producto eliminado)"),
                r.IsActive))
            .ToList()
            .AsReadOnly();
    }
}
