using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Products.DTOs;

namespace RushOrder.Application.Products.Queries;

public record SearchProductsQuery(Guid RestaurantId, string SearchTerm) : IQuery<List<ProductSummaryDto>>;

public sealed class SearchProductsQueryValidator : AbstractValidator<SearchProductsQuery>
{
    public SearchProductsQueryValidator()
    {
        RuleFor(x => x.RestaurantId).NotEmpty();
        RuleFor(x => x.SearchTerm).NotEmpty().MinimumLength(2).MaximumLength(100);
    }
}

public sealed class SearchProductsQueryHandler : IRequestHandler<SearchProductsQuery, List<ProductSummaryDto>>
{
    private readonly IProductRepository _productRepository;

    public SearchProductsQueryHandler(IProductRepository productRepository)
        => _productRepository = productRepository;

    public async Task<List<ProductSummaryDto>> Handle(SearchProductsQuery request, CancellationToken cancellationToken)
    {
        var products = await _productRepository.SearchAsync(request.RestaurantId, request.SearchTerm, cancellationToken);

        return products
            .Select(p => new ProductSummaryDto(
                p.Id, p.CategoryId, p.Name, p.Description,
                p.Price.Amount, p.Price.Currency,
                p.ImageUrl, p.IsAvailable, p.SortOrder, p.PreparationMinutes))
            .ToList();
    }
}
