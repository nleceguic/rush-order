using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Orders.DTOs;
using RushOrder.Domain.Entities;
using RushOrder.Domain.Enums;

namespace RushOrder.Application.Orders.Commands;

// --- Input model ---

public record CreateOrderItemInput(
    Guid ProductId,
    int Quantity,
    string? Notes = null,
    IEnumerable<string>? Modifiers = null);

// --- Command ---

public record CreateOrderCommand(
    Guid TableId,
    Guid? CustomerId,
    IReadOnlyList<CreateOrderItemInput> Items,
    string? Notes,
    OrderSource Source) : ICommand<CreateOrderResult>;

// --- Validator ---

public sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.TableId)
            .NotEmpty().WithMessage("TableId is required.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("At least one item is required.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId)
                .NotEmpty().WithMessage("ProductId is required.");
            item.RuleFor(i => i.Quantity)
                .InclusiveBetween(1, 50).WithMessage("Quantity must be between 1 and 50.");
        });
    }
}

// --- Handler ---

public sealed class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, CreateOrderResult>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;
    private readonly ITableRepository _tableRepository;
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenantService _tenantService;

    public CreateOrderCommandHandler(
        IOrderRepository orderRepository,
        IProductRepository productRepository,
        ITableRepository tableRepository,
        IRestaurantRepository restaurantRepository,
        IUnitOfWork unitOfWork,
        ICurrentTenantService tenantService)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
        _tableRepository = tableRepository;
        _restaurantRepository = restaurantRepository;
        _unitOfWork = unitOfWork;
        _tenantService = tenantService;
    }

    public async Task<CreateOrderResult> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantService.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context is required.");

        var table = await _tableRepository.GetByIdAsync(request.TableId, cancellationToken)
            ?? throw new NotFoundException(nameof(Table), request.TableId);

        if (table.Status != TableStatus.Free && table.Status != TableStatus.Occupied)
            throw new BusinessRuleException($"Table is not available for orders. Current status: {table.Status}.");

        var restaurant = await _restaurantRepository.GetByIdAsync(table.RestaurantId, cancellationToken)
            ?? throw new NotFoundException(nameof(Restaurant), table.RestaurantId);

        var sequenceNumber = await _orderRepository.GetNextSequenceNumberAsync(restaurant.Id, cancellationToken);

        var order = Order.Create(
            tenantId,
            restaurant.Id,
            table.Id,
            sequenceNumber,
            request.Source,
            restaurant.TaxRate,
            restaurant.Currency,
            request.CustomerId,
            notes: request.Notes);

        foreach (var itemInput in request.Items)
        {
            var product = await _productRepository.GetByIdAsync(itemInput.ProductId, cancellationToken)
                ?? throw new NotFoundException(nameof(Product), itemInput.ProductId);

            if (!product.IsAvailable)
                throw new BusinessRuleException($"Product '{product.Name}' is not available.");

            order.AddItem(
                product.Id,
                product.Name,
                product.Price,
                itemInput.Quantity,
                itemInput.Notes,
                itemInput.Modifiers);
        }

        await _orderRepository.AddAsync(order, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateOrderResult(order.Id, order.OrderNumber, order.EstimatedReadyAt);
    }
}
