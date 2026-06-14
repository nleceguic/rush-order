using MediatR;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Menu.DTOs;

namespace RushOrder.Application.Menu.Queries;

public record GetPublicMenuQuery(string QrToken) : IQuery<PublicMenuDto?>;

public sealed class GetPublicMenuQueryHandler : IRequestHandler<GetPublicMenuQuery, PublicMenuDto?>
{
    private readonly ITableRepository _tableRepository;
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IProductRepository _productRepository;
    private readonly IMenuCacheService _cacheService;

    public GetPublicMenuQueryHandler(
        ITableRepository tableRepository,
        IRestaurantRepository restaurantRepository,
        ICategoryRepository categoryRepository,
        IProductRepository productRepository,
        IMenuCacheService cacheService)
    {
        _tableRepository = tableRepository;
        _restaurantRepository = restaurantRepository;
        _categoryRepository = categoryRepository;
        _productRepository = productRepository;
        _cacheService = cacheService;
    }

    public async Task<PublicMenuDto?> Handle(GetPublicMenuQuery request, CancellationToken cancellationToken)
    {
        var cached = await _cacheService.GetMenuAsync(request.QrToken, cancellationToken);
        if (cached is not null) return cached;

        var table = await _tableRepository.GetByQrCodeAsync(request.QrToken, cancellationToken);
        if (table is null) return null;

        var restaurant = await _restaurantRepository.GetByIdAsync(table.RestaurantId, cancellationToken);
        if (restaurant is null || !restaurant.IsActive) return null;

        var categories = await _categoryRepository.GetByRestaurantPublicAsync(restaurant.Id, cancellationToken);
        var allProducts = await _productRepository.GetByRestaurantPublicAsync(restaurant.Id, onlyAvailable: true, cancellationToken: cancellationToken);

        var productsByCategory = allProducts
            .GroupBy(p => p.CategoryId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var menuCategories = categories
            .Select(c => new PublicMenuCategoryDto(
                c.Id,
                c.Name,
                c.ImageUrl,
                (productsByCategory.TryGetValue(c.Id, out var products)
                    ? products
                    : [])
                .Select(p => new PublicMenuProductDto(
                    p.Id, p.Name, p.Description,
                    p.Price.Amount, p.Price.Currency,
                    p.ImageUrl,
                    p.Allergens,
                    p.Tags.Select(t => t.ToString()).ToList().AsReadOnly(),
                    p.IsAvailable,
                    p.PreparationMinutes))
                .ToList()
                .AsReadOnly()))
            .ToList()
            .AsReadOnly();

        var menu = new PublicMenuDto(
            restaurant.Id,
            new RestaurantInfoDto(restaurant.Name, restaurant.LogoUrl, restaurant.CoverUrl, null),
            menuCategories,
            []);

        await _cacheService.SetMenuAsync(request.QrToken, menu, TimeSpan.FromSeconds(30), cancellationToken);

        return menu;
    }
}
