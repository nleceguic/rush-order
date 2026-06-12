using RushOrder.Domain.Common;
using RushOrder.Domain.Enums;
using RushOrder.Domain.ValueObjects;

namespace RushOrder.Domain.Entities;

public sealed class Tenant : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public Guid PlanId { get; private set; }
    public TenantStatus Status { get; private set; }
    public DateTimeOffset? TrialEndsAt { get; private set; }
    public TenantSettings Settings { get; private set; } = TenantSettings.Default;
    public BillingInfo BillingInfo { get; private set; } = BillingInfo.Empty;

    private Tenant() { } // EF Core

    private Tenant(string name, string slug, Guid planId, DateTimeOffset? trialEndsAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        if (planId == Guid.Empty) throw new ArgumentException("PlanId cannot be empty.", nameof(planId));

        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        PlanId = planId;
        Status = trialEndsAt.HasValue ? TenantStatus.Trial : TenantStatus.Active;
        TrialEndsAt = trialEndsAt;
    }

    public static Tenant Create(string name, string slug, Guid planId, DateTimeOffset? trialEndsAt = null)
        => new(name, slug, planId, trialEndsAt);

    public void UpdateSettings(TenantSettings settings)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateBillingInfo(BillingInfo billingInfo)
    {
        BillingInfo = billingInfo ?? throw new ArgumentNullException(nameof(billingInfo));
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Activate()
    {
        Status = TenantStatus.Active;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Suspend()
    {
        if (Status == TenantStatus.Cancelled)
            throw new InvalidOperationException("Cannot suspend a cancelled tenant.");
        Status = TenantStatus.Suspended;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Cancel()
    {
        Status = TenantStatus.Cancelled;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
