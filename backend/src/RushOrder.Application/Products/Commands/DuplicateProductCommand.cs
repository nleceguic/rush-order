using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;
using RushOrder.Domain.Enums;

namespace RushOrder.Application.Products.Commands;

public record DuplicateProductCommand(Guid ProductId, string NewName) : ICommand<Guid>;

public sealed class DuplicateProductCommandValidator : AbstractValidator<DuplicateProductCommand>
{
    public DuplicateProductCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.NewName).NotEmpty().MaximumLength(200);
    }
}

public sealed class DuplicateProductCommandHandler : IRequestHandler<DuplicateProductCommand, Guid>
{
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DuplicateProductCommandHandler(IProductRepository productRepository, IUnitOfWork unitOfWork)
    {
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(DuplicateProductCommand request, CancellationToken cancellationToken)
    {
        var source = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        var duplicate = Product.Create(
            source.TenantId,
            source.RestaurantId,
            source.CategoryId,
            request.NewName,
            source.Price,
            source.PreparationMinutes);

        duplicate.UpdateDetails(request.NewName, source.Description, source.Price, source.PreparationMinutes);
        duplicate.SetImage(source.ImageUrl);
        duplicate.SetSortOrder(source.SortOrder + 1);

        foreach (var allergen in source.Allergens)
            duplicate.AddAllergen(allergen);

        foreach (var tag in source.Tags)
            duplicate.AddTag(tag);

        await _productRepository.AddAsync(duplicate, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return duplicate.Id;
    }
}
