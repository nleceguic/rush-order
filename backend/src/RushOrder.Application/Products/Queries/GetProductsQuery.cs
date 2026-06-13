using MediatR;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Common.Models;
using RushOrder.Application.Products.DTOs;

namespace RushOrder.Application.Products.Queries;

public record GetProductsQuery(
    Guid RestaurantId,
    Guid? CategoryId,
    bool? OnlyAvailable,
    int PageNumber,
    int PageSize) : IQuery<PagedResult<ProductSummaryDto>>;

public sealed class GetProductsQueryHandler : IRequestHandler<GetProductsQuery, PagedResult<ProductSummaryDto>>
{
    private readonly IProductRepository _productRepository;

    public GetProductsQueryHandler(IProductRepository productRepository)
        => _productRepository = productRepository;

    public async Task<PagedResult<ProductSummaryDto>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        var totalCount = await _productRepository.CountByRestaurantAsync(
            request.RestaurantId, request.CategoryId, request.OnlyAvailable, cancellationToken);

        var products = await _productRepository.GetPagedByRestaurantAsync(
            request.RestaurantId, request.CategoryId, request.OnlyAvailable,
            request.PageNumber, request.PageSize, cancellationToken);

        var items = products
            .Select(p => new ProductSummaryDto(
                p.Id, p.CategoryId, p.Name, p.Description,
                p.Price.Amount, p.Price.Currency,
                p.ImageUrl, p.IsAvailable, p.SortOrder, p.PreparationMinutes))
            .ToList()
            .AsReadOnly();

        return new PagedResult<ProductSummaryDto>(items, totalCount, request.PageNumber, request.PageSize);
    }
}
