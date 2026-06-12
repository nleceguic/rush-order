namespace RushOrder.Domain.Common;

public abstract class TenantEntity : BaseEntity
{
    public Guid TenantId { get; private set; }

    protected TenantEntity(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        TenantId = tenantId;
    }

    // For EF Core
    protected TenantEntity() { }
}
