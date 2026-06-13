using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Customers.Commands;

public record UpdateCustomerProfileCommand(
    Guid CustomerId,
    string? FirstName,
    string? LastName,
    string? Email,
    string? Phone) : ICommand<Unit>;

public sealed class UpdateCustomerProfileCommandValidator : AbstractValidator<UpdateCustomerProfileCommand>
{
    public UpdateCustomerProfileCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.FirstName).MaximumLength(100).When(x => x.FirstName is not null);
        RuleFor(x => x.LastName).MaximumLength(100).When(x => x.LastName is not null);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(200).When(x => x.Email is not null);
        RuleFor(x => x.Phone).MaximumLength(50).When(x => x.Phone is not null);
    }
}

public sealed class UpdateCustomerProfileCommandHandler : IRequestHandler<UpdateCustomerProfileCommand, Unit>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCustomerProfileCommandHandler(ICustomerRepository customerRepository, IUnitOfWork unitOfWork)
    {
        _customerRepository = customerRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(UpdateCustomerProfileCommand request, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByIdAsync(request.CustomerId, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.CustomerId);

        customer.UpdateProfile(request.FirstName, request.LastName, request.Email, request.Phone);

        await _customerRepository.UpdateAsync(customer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
