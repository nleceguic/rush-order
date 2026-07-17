using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Recommendations.Commands;

public record CreatePairingRuleCommand(
    Guid RestaurantId,
    Guid SourceProductId,
    Guid TargetProductId) : ICommand<Guid>;

public sealed class CreatePairingRuleCommandValidator : AbstractValidator<CreatePairingRuleCommand>
{
    public CreatePairingRuleCommandValidator()
    {
        RuleFor(x => x.RestaurantId).NotEmpty();
        RuleFor(x => x.SourceProductId).NotEmpty();
        RuleFor(x => x.TargetProductId).NotEmpty();
        RuleFor(x => x)
            .Must(x => x.SourceProductId != x.TargetProductId)
            .WithMessage("A product cannot be paired with itself.");
    }
}

public sealed class CreatePairingRuleCommandHandler : IRequestHandler<CreatePairingRuleCommand, Guid>
{
    private readonly IPairingRuleRepository _pairingRules;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenantService _tenant;

    public CreatePairingRuleCommandHandler(
        IPairingRuleRepository pairingRules, IUnitOfWork unitOfWork, ICurrentTenantService tenant)
    {
        _pairingRules = pairingRules;
        _unitOfWork = unitOfWork;
        _tenant = tenant;
    }

    public async Task<Guid> Handle(CreatePairingRuleCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenant.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

        var rule = ProductPairingRule.Create(
            tenantId, request.RestaurantId, request.SourceProductId, request.TargetProductId);

        await _pairingRules.AddAsync(rule, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return rule.Id;
    }
}
