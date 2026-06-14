using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;

namespace RushOrder.Application.Admin.Commands;

public sealed record ActivateTenantCommand(Guid TenantId) : ICommand<Unit>;

public sealed class ActivateTenantCommandHandler : IRequestHandler<ActivateTenantCommand, Unit>
{
    private readonly ITenantRepository _tenantRepo;
    private readonly IUnitOfWork _unitOfWork;

    public ActivateTenantCommandHandler(ITenantRepository tenantRepo, IUnitOfWork unitOfWork)
    {
        _tenantRepo = tenantRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(ActivateTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepo.GetByIdAsync(request.TenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant", request.TenantId);

        tenant.Activate();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
