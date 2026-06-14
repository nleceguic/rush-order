using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Subscriptions.DTOs;

namespace RushOrder.Application.Subscriptions.Queries;

public sealed record GetUsageQuery : IRequest<UsageDto>;

public sealed class GetUsageQueryHandler : IRequestHandler<GetUsageQuery, UsageDto>
{
    private readonly ICurrentTenantService _currentTenant;
    private readonly ISubscriptionRepository _subscriptionRepo;
    private readonly IPlanRepository _planRepo;
    private readonly IRestaurantRepository _restaurantRepo;
    private readonly ITableRepository _tableRepo;
    private readonly IUserRepository _userRepo;

    public GetUsageQueryHandler(
        ICurrentTenantService currentTenant,
        ISubscriptionRepository subscriptionRepo,
        IPlanRepository planRepo,
        IRestaurantRepository restaurantRepo,
        ITableRepository tableRepo,
        IUserRepository userRepo)
    {
        _currentTenant = currentTenant;
        _subscriptionRepo = subscriptionRepo;
        _planRepo = planRepo;
        _restaurantRepo = restaurantRepo;
        _tableRepo = tableRepo;
        _userRepo = userRepo;
    }

    public async Task<UsageDto> Handle(GetUsageQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId
            ?? throw new BusinessRuleException("Tenant context is required.");

        var subscription = await _subscriptionRepo.GetByTenantIdAsync(tenantId, cancellationToken)
            ?? throw new NotFoundException("Subscription", tenantId);

        var plan = await _planRepo.GetByIdAsync(subscription.PlanId, cancellationToken)
            ?? throw new NotFoundException("Plan", subscription.PlanId);

        var restaurants = await _restaurantRepo.GetAllAsync(cancellationToken);
        var tables = await _tableRepo.GetAllAsync(cancellationToken);
        var users = await _userRepo.GetAllAsync(cancellationToken);

        return new UsageDto(
            TablesUsed: tables.Count,
            MaxTables: plan.MaxTables,
            UsersUsed: users.Count,
            MaxUsers: plan.MaxUsers,
            RestaurantsUsed: restaurants.Count,
            MaxRestaurants: plan.MaxRestaurants);
    }
}
