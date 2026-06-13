using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Products.Commands;

public record UpdateStockResult(int CurrentQuantity, bool IsLowStock, int LowStockThreshold);

public record UpdateStockCommand(Guid ProductId, int NewQuantity) : ICommand<UpdateStockResult>;

public sealed class UpdateStockCommandValidator : AbstractValidator<UpdateStockCommand>
{
    public UpdateStockCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.NewQuantity).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateStockCommandHandler : IRequestHandler<UpdateStockCommand, UpdateStockResult>
{
    private const int LowStockThreshold = 5;

    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateStockCommandHandler(IProductRepository productRepository, IUnitOfWork unitOfWork)
    {
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<UpdateStockResult> Handle(UpdateStockCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        if (!product.StockTracking)
            throw new BusinessRuleException($"Product '{product.Name}' does not have stock tracking enabled.");

        var delta = request.NewQuantity - (product.StockQuantity ?? 0);
        product.AdjustStock(delta);

        await _productRepository.UpdateAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new UpdateStockResult(
            product.StockQuantity ?? 0,
            product.StockQuantity <= LowStockThreshold,
            LowStockThreshold);
    }
}
