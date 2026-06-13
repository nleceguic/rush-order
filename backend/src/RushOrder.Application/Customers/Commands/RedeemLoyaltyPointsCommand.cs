using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Customers.Commands;

public record RedeemLoyaltyPointsCommand(Guid CustomerId, int Points, Guid OrderId) : ICommand<int>;

public sealed class RedeemLoyaltyPointsCommandValidator : AbstractValidator<RedeemLoyaltyPointsCommand>
{
    public RedeemLoyaltyPointsCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Points).GreaterThan(0);
        RuleFor(x => x.OrderId).NotEmpty();
    }
}

public sealed class RedeemLoyaltyPointsCommandHandler : IRequestHandler<RedeemLoyaltyPointsCommand, int>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RedeemLoyaltyPointsCommandHandler(ICustomerRepository customerRepository, IUnitOfWork unitOfWork)
    {
        _customerRepository = customerRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<int> Handle(RedeemLoyaltyPointsCommand request, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByIdAsync(request.CustomerId, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.CustomerId);

        customer.RedeemPoints(request.Points);

        await _customerRepository.UpdateAsync(customer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return customer.LoyaltyPoints;
    }
}
