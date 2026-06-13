using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;
using RushOrder.Domain.Enums;

namespace RushOrder.Application.Orders.Commands;

// --- Command ---

public record UpdateOrderStatusCommand(
    Guid OrderId,
    OrderStatus NewStatus,
    string? Note) : ICommand<Unit>;

// --- Validator ---

public sealed class UpdateOrderStatusCommandValidator : AbstractValidator<UpdateOrderStatusCommand>
{
    public UpdateOrderStatusCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.NewStatus).IsInEnum();
        When(x => x.NewStatus == OrderStatus.Cancelled, () =>
            RuleFor(x => x.Note).NotEmpty().WithMessage("A cancellation note is required."));
    }
}

// --- Handler ---

public sealed class UpdateOrderStatusCommandHandler : IRequestHandler<UpdateOrderStatusCommand, Unit>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateOrderStatusCommandHandler(IOrderRepository orderRepository, IUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(UpdateOrderStatusCommand request, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), request.OrderId);

        // Domain methods enforce valid state transitions and raise OrderStatusChangedEvent
        switch (request.NewStatus)
        {
            case OrderStatus.Confirmed:
                order.Confirm();
                break;
            case OrderStatus.Preparing:
                order.StartPreparation();
                break;
            case OrderStatus.Ready:
                order.MarkReady();
                break;
            case OrderStatus.Served:
                order.MarkServed();
                break;
            case OrderStatus.Paid:
                order.Pay();
                break;
            case OrderStatus.Cancelled:
                order.Cancel(request.Note!);
                break;
            default:
                throw new BusinessRuleException($"Cannot manually transition to status '{request.NewStatus}'.");
        }

        await _orderRepository.UpdateAsync(order, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
