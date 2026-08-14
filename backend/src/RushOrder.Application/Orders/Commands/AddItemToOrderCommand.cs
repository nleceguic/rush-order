using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;
using RushOrder.Domain.Enums;

namespace RushOrder.Application.Orders.Commands;

// --- Command ---

public record AddItemToOrderCommand(
    Guid OrderId,
    Guid ProductId,
    int Quantity,
    string? Notes = null) : ICommand<Unit>;

// --- Validator ---

public sealed class AddItemToOrderCommandValidator : AbstractValidator<AddItemToOrderCommand>
{
    public AddItemToOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).InclusiveBetween(1, 50);
    }
}

// --- Handler ---

public sealed class AddItemToOrderCommandHandler : IRequestHandler<AddItemToOrderCommand, Unit>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenantService _tenantService;

    public AddItemToOrderCommandHandler(
        IOrderRepository orderRepository,
        IProductRepository productRepository,
        IUnitOfWork unitOfWork,
        ICurrentTenantService tenantService)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
        _tenantService = tenantService;
    }

    public async Task<Unit> Handle(AddItemToOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), request.OrderId);

        if (_tenantService.IsQrSession && _tenantService.QrSessionTableId != order.TableId)
            throw new UnauthorizedAccessException("QR session does not match the order's table.");

        if (order.Status != OrderStatus.Pending && order.Status != OrderStatus.Confirmed)
            throw new BusinessRuleException(
                $"Items can only be added to Pending or Confirmed orders. Current status: {order.Status}.");

        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        if (!product.IsAvailable)
            throw new BusinessRuleException($"Product '{product.Name}' is not available.");

        order.AddItem(product.Id, product.Name, product.Price, request.Quantity, request.Notes);

        await _orderRepository.UpdateAsync(order, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
