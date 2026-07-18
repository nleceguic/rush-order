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

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        await SetTenantContextAsync(command, cancellationToken);
        return result;
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        SetTenantContext(command);
        return result;
    }

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        await SetTenantContextAsync(command, cancellationToken);
        return result;
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        SetTenantContext(command);
        return result;
    }

    public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        await SetTenantContextAsync(command, cancellationToken);
        return result;
    }

    // Issued as its own command (not concatenated into `command.CommandText`) because
    // prepending text to a batched multi-statement modification command shifts Npgsql's
    // NextResult()/RecordsAffected alignment, corrupting EF's per-row concurrency check
    // (surfaces as spurious DbUpdateConcurrencyException on multi-row SaveChanges batches).
    // Session-scoped SET (not SET LOCAL) is safe here because this runs before every
    // command on the connection, so it's always refreshed before any tenant-sensitive query.
    private void SetTenantContext(DbCommand command)
    {
        if (command.Connection is null) return;

        var tenantId = _currentTenant.TenantId?.ToString() ?? Guid.Empty.ToString();
        using var setCmd = command.Connection.CreateCommand();
        setCmd.Transaction = command.Transaction;
        setCmd.CommandText = $"SET app.current_tenant_id = '{tenantId}';";
        setCmd.ExecuteNonQuery();
    }

    private async Task SetTenantContextAsync(DbCommand command, CancellationToken cancellationToken)
    {
        if (command.Connection is null) return;

        var tenantId = _currentTenant.TenantId?.ToString() ?? Guid.Empty.ToString();
        await using var setCmd = command.Connection.CreateCommand();
        setCmd.Transaction = command.Transaction;
        setCmd.CommandText = $"SET app.current_tenant_id = '{tenantId}';";
        await setCmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
