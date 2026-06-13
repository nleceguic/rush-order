using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Tables.Commands;

public record DeleteTableCommand(Guid TableId) : ICommand<Unit>;

public sealed class DeleteTableCommandValidator : AbstractValidator<DeleteTableCommand>
{
    public DeleteTableCommandValidator()
    {
        RuleFor(x => x.TableId).NotEmpty();
    }
}

public sealed class DeleteTableCommandHandler : IRequestHandler<DeleteTableCommand, Unit>
{
    private readonly ITableRepository _tableRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteTableCommandHandler(
        ITableRepository tableRepository,
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork)
    {
        _tableRepository = tableRepository;
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(DeleteTableCommand request, CancellationToken cancellationToken)
    {
        var table = await _tableRepository.GetByIdAsync(request.TableId, cancellationToken)
            ?? throw new NotFoundException(nameof(Table), request.TableId);

        var activeOrders = await _orderRepository.GetActiveByTableAsync(request.TableId, cancellationToken);
        if (activeOrders.Count > 0)
            throw new BusinessRuleException($"Cannot delete table '{table.Name}': it has {activeOrders.Count} active order(s).");

        await _tableRepository.DeleteAsync(table, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
