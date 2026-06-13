using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Customers.DTOs;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Customers.Queries;

public record GetOrCreateCustomerByDeviceQuery(
    string DeviceFingerprint,
    string? Email = null) : IQuery<CustomerDto>;

public sealed class GetOrCreateCustomerByDeviceQueryValidator : AbstractValidator<GetOrCreateCustomerByDeviceQuery>
{
    public GetOrCreateCustomerByDeviceQueryValidator()
    {
        RuleFor(x => x.DeviceFingerprint).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Email).EmailAddress().When(x => x.Email is not null);
    }
}

public sealed class GetOrCreateCustomerByDeviceQueryHandler
    : IRequestHandler<GetOrCreateCustomerByDeviceQuery, CustomerDto>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenantService _tenantService;

    public GetOrCreateCustomerByDeviceQueryHandler(
        ICustomerRepository customerRepository,
        IUnitOfWork unitOfWork,
        ICurrentTenantService tenantService)
    {
        _customerRepository = customerRepository;
        _unitOfWork = unitOfWork;
        _tenantService = tenantService;
    }

    public async Task<CustomerDto> Handle(GetOrCreateCustomerByDeviceQuery request, CancellationToken cancellationToken)
    {
        // Check by device fingerprint first (most common path)
        var existing = await _customerRepository.GetByDeviceFingerprintAsync(request.DeviceFingerprint, cancellationToken);
        if (existing is not null)
            return ToDto(existing);

        // If email provided, check if a customer with this email exists → associate device
        if (request.Email is not null)
        {
            var byEmail = await _customerRepository.GetByEmailAsync(request.Email, cancellationToken);
            if (byEmail is not null)
            {
                byEmail.AssociateDevice(request.DeviceFingerprint);
                await _customerRepository.UpdateAsync(byEmail, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return ToDto(byEmail);
            }
        }

        // Create anonymous customer
        var tenantId = _tenantService.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context is required.");

        var customer = Customer.Create(tenantId, request.DeviceFingerprint);
        if (request.Email is not null)
            customer.UpdateProfile(null, null, request.Email, null);

        await _customerRepository.AddAsync(customer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(customer);
    }

    private static CustomerDto ToDto(Customer c) => new(
        c.Id,
        c.DisplayName,
        c.Email?.Value,
        c.Phone,
        c.FirstName,
        c.LastName,
        c.LoyaltyPoints,
        c.LoyaltyLevel.ToString(),
        c.CreatedAt);
}
