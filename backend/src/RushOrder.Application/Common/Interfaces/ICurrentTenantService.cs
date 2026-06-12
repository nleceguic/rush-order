namespace RushOrder.Application.Common.Interfaces;

public interface ICurrentTenantService
{
    Guid? TenantId { get; }
    bool IsAuthenticated { get; }
}
