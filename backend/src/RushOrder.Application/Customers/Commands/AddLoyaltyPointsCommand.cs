using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Customers.Commands;

public record AddLoyaltyPointsResult(int NewTotal, string LoyaltyLevel, bool LevelChanged);

public record AddLoyaltyPointsCommand(Guid CustomerId, int Points, Guid OrderId) : ICommand<AddLoyaltyPointsResult>;

public sealed class AddLoyaltyPointsCommandValidator : AbstractValidator<AddLoyaltyPointsCommand>
{
    public AddLoyaltyPointsCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Points).GreaterThan(0);
        RuleFor(x => x.OrderId).NotEmpty();
    }
}

public sealed class AddLoyaltyPointsCommandHandler : IRequestHandler<AddLoyaltyPointsCommand, AddLoyaltyPointsResult>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AddLoyaltyPointsCommandHandler(ICustomerRepository customerRepository, IUnitOfWork unitOfWork)
    {
        _customerRepository = customerRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<AddLoyaltyPointsResult> Handle(AddLoyaltyPointsCommand request, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByIdAsync(request.CustomerId, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.CustomerId);

        var previousLevel = customer.LoyaltyLevel;
        customer.AddLoyaltyPoints(request.Points);
        var levelChanged = customer.LoyaltyLevel != previousLevel;

        await _customerRepository.UpdateAsync(customer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AddLoyaltyPointsResult(customer.LoyaltyPoints, customer.LoyaltyLevel.ToString(), levelChanged);
    }
}
