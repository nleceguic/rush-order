using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;

namespace RushOrder.Application.Admin.Commands;

public sealed record SuspendTenantCommand(Guid TenantId, string Reason) : ICommand<Unit>;

public sealed class SuspendTenantCommandHandler : IRequestHandler<SuspendTenantCommand, Unit>
{
    private readonly ITenantRepository _tenantRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notifications;

    public SuspendTenantCommandHandler(
        ITenantRepository tenantRepo,
        IUnitOfWork unitOfWork,
        INotificationService notifications)
    {
        _tenantRepo = tenantRepo;
        _unitOfWork = unitOfWork;
        _notifications = notifications;
    }

    public async Task<Unit> Handle(SuspendTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepo.GetByIdAsync(request.TenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant", request.TenantId);

        tenant.Suspend();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _ = TryNotifyAsync(tenant.BillingInfo.InvoiceEmail, tenant.Name, cancellationToken);
        return Unit.Value;
    }

    private async Task TryNotifyAsync(string? email, string tenantName, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrEmpty(email)) return;
            await _notifications.SendSubscriptionSuspendedAsync(email, tenantName, ct);
        }
        catch { /* best-effort */ }
    }
}
