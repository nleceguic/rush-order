using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;
using RushOrder.Domain.Enums;
using RushOrder.Domain.ValueObjects;

namespace RushOrder.Application.Products.Commands;

public record CreateProductCommand(
    Guid CategoryId,
    string Name,
    string? Description,
    decimal Price,
    string? ImageUrl,
    IReadOnlyList<string> Allergens,
    IReadOnlyList<string> Tags,
    int PreparationMinutes) : ICommand<Guid>;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Price).GreaterThan(0).WithMessage("Price must be greater than 0.");
        RuleFor(x => x.Description).MaximumLength(1000).When(x => x.Description is not null);
        RuleFor(x => x.ImageUrl).MaximumLength(500).When(x => x.ImageUrl is not null);
        RuleFor(x => x.PreparationMinutes).GreaterThanOrEqualTo(0);
    }
}

public sealed class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Guid>
{
    private readonly IProductRepository _productRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenantService _tenantService;

    public CreateProductCommandHandler(
        IProductRepository productRepository,
        ICategoryRepository categoryRepository,
        IRestaurantRepository restaurantRepository,
        IUnitOfWork unitOfWork,
        ICurrentTenantService tenantService)
    {
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
        _restaurantRepository = restaurantRepository;
        _unitOfWork = unitOfWork;
        _tenantService = tenantService;
    }

    public async Task<Guid> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantService.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context is required.");

        var category = await _categoryRepository.GetByIdAsync(request.CategoryId, cancellationToken)
            ?? throw new NotFoundException(nameof(Category), request.CategoryId);

        var restaurant = await _restaurantRepository.GetByIdAsync(category.RestaurantId, cancellationToken)
            ?? throw new NotFoundException(nameof(Restaurant), category.RestaurantId);

        var product = Product.Create(
            tenantId,
            restaurant.Id,
            category.Id,
            request.Name,
            new Money(request.Price, restaurant.Currency),
            request.PreparationMinutes);

        if (request.Description is not null)
            product.UpdateDetails(product.Name, request.Description, product.Price, product.PreparationMinutes);

        if (request.ImageUrl is not null)
            product.SetImage(request.ImageUrl);

        foreach (var allergen in request.Allergens)
            product.AddAllergen(allergen);

        foreach (var tagStr in request.Tags)
            if (Enum.TryParse<ProductTag>(tagStr, ignoreCase: true, out var tag))
                product.AddTag(tag);

        await _productRepository.AddAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return product.Id;
    }
}
