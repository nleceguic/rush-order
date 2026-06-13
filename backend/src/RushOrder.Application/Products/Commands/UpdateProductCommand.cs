using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;
using RushOrder.Domain.Enums;
using RushOrder.Domain.ValueObjects;

namespace RushOrder.Application.Products.Commands;

public record UpdateProductCommand(
    Guid ProductId,
    string? Name = null,
    string? Description = null,
    decimal? Price = null,
    string? ImageUrl = null,
    IReadOnlyList<string>? Allergens = null,
    IReadOnlyList<string>? Tags = null,
    int? PreparationMinutes = null,
    int? SortOrder = null) : ICommand<Unit>;

public sealed class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).When(x => x.Name is not null);
        RuleFor(x => x.Price).GreaterThan(0).When(x => x.Price.HasValue);
        RuleFor(x => x.PreparationMinutes).GreaterThanOrEqualTo(0).When(x => x.PreparationMinutes.HasValue);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0).When(x => x.SortOrder.HasValue);
    }
}

public sealed class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, Unit>
{
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateProductCommandHandler(IProductRepository productRepository, IUnitOfWork unitOfWork)
    {
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        var newPrice = request.Price.HasValue
            ? new Money(request.Price.Value, product.Price.Currency)
            : product.Price;

        product.UpdateDetails(
            request.Name ?? product.Name,
            request.Description ?? product.Description,
            newPrice,
            request.PreparationMinutes ?? product.PreparationMinutes);

        if (request.ImageUrl is not null)
            product.SetImage(request.ImageUrl);

        if (request.SortOrder.HasValue)
            product.SetSortOrder(request.SortOrder.Value);

        if (request.Allergens is not null)
        {
            foreach (var allergen in product.Allergens.Where(a => !request.Allergens.Contains(a)).ToList())
                product.RemoveAllergen(allergen);
            foreach (var allergen in request.Allergens.Where(a => !product.Allergens.Contains(a)))
                product.AddAllergen(allergen);
        }

        if (request.Tags is not null)
        {
            var newTags = request.Tags
                .Where(t => Enum.TryParse<ProductTag>(t, ignoreCase: true, out _))
                .Select(t => Enum.Parse<ProductTag>(t, ignoreCase: true))
                .ToList();
            foreach (var tag in product.Tags.Where(t => !newTags.Contains(t)).ToList())
                product.RemoveTag(tag);
            foreach (var tag in newTags.Where(t => !product.Tags.Contains(t)))
                product.AddTag(tag);
        }

        await _productRepository.UpdateAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
