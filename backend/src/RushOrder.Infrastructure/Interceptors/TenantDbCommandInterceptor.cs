using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RushOrder.Application.Common.Interfaces;

namespace RushOrder.Infrastructure.Interceptors;

public sealed class TenantDbCommandInterceptor : DbCommandInterceptor
{
    private readonly ICurrentTenantService _currentTenant;

    public TenantDbCommandInterceptor(ICurrentTenantService currentTenant)
    {
        _currentTenant = currentTenant;
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        SetTenantContext(command);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        SetTenantContext(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        SetTenantContext(command);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        SetTenantContext(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        SetTenantContext(command);
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        SetTenantContext(command);
        return ValueTask.FromResult(result);
    }

    private void SetTenantContext(DbCommand command)
    {
        var tenantId = _currentTenant.TenantId?.ToString() ?? Guid.Empty.ToString();
        // Prepend SET LOCAL so it's scoped to the current transaction/statement
        command.CommandText = $"SET LOCAL app.current_tenant_id = '{tenantId}';\n{command.CommandText}";
    }
}
