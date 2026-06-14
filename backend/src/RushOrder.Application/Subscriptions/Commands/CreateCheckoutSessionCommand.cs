using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Subscriptions.DTOs;

namespace RushOrder.Application.Subscriptions.Commands;

public sealed record CreateCheckoutSessionCommand(
    Guid PlanId,
    string BillingPeriod,
    string SuccessUrl,
    string CancelUrl) : ICommand<CheckoutSessionDto>;

public sealed class CreateCheckoutSessionCommandValidator : AbstractValidator<CreateCheckoutSessionCommand>
{
    private static readonly string[] _validPeriods = ["monthly", "yearly"];

    public CreateCheckoutSessionCommandValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty();
        RuleFor(x => x.BillingPeriod).Must(p => _validPeriods.Contains(p))
            .WithMessage("BillingPeriod must be 'monthly' or 'yearly'.");
        RuleFor(x => x.SuccessUrl).NotEmpty();
        RuleFor(x => x.CancelUrl).NotEmpty();
    }
}

public sealed class CreateCheckoutSessionCommandHandler
    : IRequestHandler<CreateCheckoutSessionCommand, CheckoutSessionDto>
{
    private readonly ICurrentTenantService _currentTenant;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IPlanRepository _planRepo;

    public CreateCheckoutSessionCommandHandler(
        ICurrentTenantService currentTenant,
        ISubscriptionService subscriptionService,
        IPlanRepository planRepo)
    {
        _currentTenant = currentTenant;
        _subscriptionService = subscriptionService;
        _planRepo = planRepo;
    }

    public async Task<CheckoutSessionDto> Handle(
        CreateCheckoutSessionCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId
            ?? throw new BusinessRuleException("Tenant context is required.");

        var plan = await _planRepo.GetByIdAsync(request.PlanId, cancellationToken)
            ?? throw new NotFoundException("Plan", request.PlanId);

        if (!plan.IsActive)
            throw new BusinessRuleException("The selected plan is no longer available.");

        var url = await _subscriptionService.CreateCheckoutSessionAsync(
            tenantId, request.PlanId, request.BillingPeriod,
            request.SuccessUrl, request.CancelUrl, cancellationToken);

        return new CheckoutSessionDto(url);
    }
}
