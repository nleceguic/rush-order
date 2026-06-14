using RushOrder.Domain.Common;
using RushOrder.Domain.Enums;

namespace RushOrder.Domain.Entities;

public sealed class Subscription : BaseEntity
{
    public Guid TenantId { get; private set; }
    public Guid PlanId { get; private set; }
    public SubscriptionStatus Status { get; private set; }
    public DateTimeOffset CurrentPeriodStart { get; private set; }
    public DateTimeOffset CurrentPeriodEnd { get; private set; }
    public string? StripeSubscriptionId { get; private set; }
    public bool CancelAtPeriodEnd { get; private set; }

    private Subscription() { }

    private Subscription(
        Guid tenantId,
        Guid planId,
        SubscriptionStatus status,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        string? stripeSubscriptionId = null)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        if (planId == Guid.Empty) throw new ArgumentException("PlanId cannot be empty.", nameof(planId));

        TenantId = tenantId;
        PlanId = planId;
        Status = status;
        CurrentPeriodStart = periodStart;
        CurrentPeriodEnd = periodEnd;
        StripeSubscriptionId = stripeSubscriptionId;
    }

    public static Subscription CreateTrial(Guid tenantId, Guid planId, DateTimeOffset trialEnd)
        => new(tenantId, planId, SubscriptionStatus.Trialing, DateTimeOffset.UtcNow, trialEnd);

    public static Subscription CreateActive(
        Guid tenantId,
        Guid planId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        string stripeSubscriptionId)
        => new(tenantId, planId, SubscriptionStatus.Active, periodStart, periodEnd, stripeSubscriptionId);

    public void Activate(DateTimeOffset periodStart, DateTimeOffset periodEnd, string stripeSubscriptionId)
    {
        Status = SubscriptionStatus.Active;
        CurrentPeriodStart = periodStart;
        CurrentPeriodEnd = periodEnd;
        StripeSubscriptionId = stripeSubscriptionId;
        CancelAtPeriodEnd = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetPastDue()
    {
        Status = SubscriptionStatus.PastDue;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Cancel(bool immediately)
    {
        if (immediately)
        {
            Status = SubscriptionStatus.Cancelled;
            CurrentPeriodEnd = DateTimeOffset.UtcNow;
        }
        else
        {
            CancelAtPeriodEnd = true;
        }
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdatePlan(Guid newPlanId)
    {
        if (newPlanId == Guid.Empty) throw new ArgumentException("PlanId cannot be empty.", nameof(newPlanId));
        PlanId = newPlanId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
