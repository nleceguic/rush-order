using MediatR;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Products.DTOs;

namespace RushOrder.Application.Products.Queries;

public record GetProductByIdQuery(Guid ProductId) : IQuery<ProductDetailDto?>;

public sealed class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, ProductDetailDto?>
{
    private readonly IProductRepository _productRepository;

    public GetProductByIdQueryHandler(IProductRepository productRepository)
        => _productRepository = productRepository;

    public async Task<ProductDetailDto?> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product is null) return null;

        return new ProductDetailDto(
            product.Id,
            product.CategoryId,
            product.RestaurantId,
            product.Name,
            product.Description,
            product.Price.Amount,
            product.Price.Currency,
            product.ImageUrl,
            product.Allergens,
            product.Tags.Select(t => t.ToString()).ToList().AsReadOnly(),
            product.IsAvailable,
            product.StockTracking,
            product.StockQuantity,
            product.SortOrder,
            product.PreparationMinutes,
            product.CreatedAt,
            product.UpdatedAt);
    }
}
