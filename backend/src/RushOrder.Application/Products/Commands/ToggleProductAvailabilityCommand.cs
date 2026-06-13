using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Products.Commands;

public record ToggleProductAvailabilityCommand(Guid ProductId) : ICommand<bool>;

public sealed class ToggleProductAvailabilityCommandValidator : AbstractValidator<ToggleProductAvailabilityCommand>
{
    public ToggleProductAvailabilityCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
    }
}

public sealed class ToggleProductAvailabilityCommandHandler : IRequestHandler<ToggleProductAvailabilityCommand, bool>
{
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ToggleProductAvailabilityCommandHandler(IProductRepository productRepository, IUnitOfWork unitOfWork)
    {
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(ToggleProductAvailabilityCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        // SetAvailability raises ProductAvailabilityChangedEvent, which the
        // ProductAvailabilityChangedEventHandler will use to invalidate the menu cache.
        product.SetAvailability(!product.IsAvailable);

        await _productRepository.UpdateAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return product.IsAvailable;
    }
}
