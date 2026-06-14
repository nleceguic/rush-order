using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;

namespace RushOrder.Application.Admin.Commands;

public sealed record UpdateTenantPlanCommand(Guid TenantId, Guid PlanId) : ICommand<Unit>;

public sealed class UpdateTenantPlanCommandHandler : IRequestHandler<UpdateTenantPlanCommand, Unit>
{
    private readonly ITenantRepository _tenantRepo;
    private readonly IPlanRepository _planRepo;
    private readonly ISubscriptionRepository _subscriptionRepo;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateTenantPlanCommandHandler(
        ITenantRepository tenantRepo,
        IPlanRepository planRepo,
        ISubscriptionRepository subscriptionRepo,
        IUnitOfWork unitOfWork)
    {
        _tenantRepo = tenantRepo;
        _planRepo = planRepo;
        _subscriptionRepo = subscriptionRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(UpdateTenantPlanCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepo.GetByIdAsync(request.TenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant", request.TenantId);

        var plan = await _planRepo.GetByIdAsync(request.PlanId, cancellationToken)
            ?? throw new NotFoundException("Plan", request.PlanId);

        tenant.ChangePlan(plan.Id);

        var subscription = await _subscriptionRepo.GetByTenantIdAsync(request.TenantId, cancellationToken);
        subscription?.UpdatePlan(plan.Id);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
