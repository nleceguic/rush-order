using MediatR;
using RushOrder.Application.Categories.DTOs;
using RushOrder.Application.Common.Interfaces;

namespace RushOrder.Application.Categories.Queries;

public record GetCategoriesQuery(Guid RestaurantId) : IQuery<List<CategoryDto>>;

public sealed class GetCategoriesQueryHandler : IRequestHandler<GetCategoriesQuery, List<CategoryDto>>
{
    private readonly ICategoryRepository _categoryRepository;

    public GetCategoriesQueryHandler(ICategoryRepository categoryRepository)
        => _categoryRepository = categoryRepository;

    public async Task<List<CategoryDto>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        var categories = await _categoryRepository.GetByRestaurantAsync(request.RestaurantId, cancellationToken);

        return categories
            .Select(c => new CategoryDto(c.Id, c.RestaurantId, c.Name, c.Description, c.ImageUrl, c.SortOrder))
            .ToList();
    }
}
