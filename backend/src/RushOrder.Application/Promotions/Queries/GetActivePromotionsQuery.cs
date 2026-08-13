using MediatR;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Menu.DTOs;

namespace RushOrder.Application.Promotions.Queries;

public record GetActivePromotionsQuery(Guid RestaurantId) : IQuery<IReadOnlyList<PromotionDto>>;

public sealed class GetActivePromotionsQueryHandler : IRequestHandler<GetActivePromotionsQuery, IReadOnlyList<PromotionDto>>
{
    private readonly IPromotionRepository _promotionRepository;

    public GetActivePromotionsQueryHandler(IPromotionRepository promotionRepository)
    {
        _promotionRepository = promotionRepository;
    }

    public async Task<IReadOnlyList<PromotionDto>> Handle(GetActivePromotionsQuery request, CancellationToken cancellationToken)
    {
        var promotions = await _promotionRepository.GetActiveByRestaurantAsync(request.RestaurantId, cancellationToken);

        return promotions
            .Select(p => new PromotionDto(p.Id, p.Name, p.Description ?? string.Empty))
            .ToList()
            .AsReadOnly();
    }
}
