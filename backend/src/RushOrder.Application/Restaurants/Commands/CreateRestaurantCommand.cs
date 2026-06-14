using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Restaurants.Commands;

public record CreateRestaurantCommand(
    string Name,
    string Address,
    string Phone,
    string Email,
    decimal TaxRate = 0.21m,
    string Timezone = "Europe/Madrid",
    string Currency = "EUR") : ICommand<Guid>;

public sealed class CreateRestaurantCommandValidator : AbstractValidator<CreateRestaurantCommand>
{
    public CreateRestaurantCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.TaxRate).InclusiveBetween(0m, 1m);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
    }
}

public sealed class CreateRestaurantCommandHandler : IRequestHandler<CreateRestaurantCommand, Guid>
{
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenantService _tenantService;

    public CreateRestaurantCommandHandler(
        IRestaurantRepository restaurantRepository,
        IUnitOfWork unitOfWork,
        ICurrentTenantService tenantService)
    {
        _restaurantRepository = restaurantRepository;
        _unitOfWork = unitOfWork;
        _tenantService = tenantService;
    }

    public async Task<Guid> Handle(CreateRestaurantCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantService.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context is required.");

        var restaurant = Restaurant.Create(
            tenantId, request.Name, request.Address, request.Phone, request.Email,
            request.TaxRate, request.Timezone, request.Currency);

        await _restaurantRepository.AddAsync(restaurant, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return restaurant.Id;
    }
}
