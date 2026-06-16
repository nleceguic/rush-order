namespace RushOrder.Desktop.Models;

public sealed record PendingSyncOperation(
    long           Id,
    string         OperationType,
    string         Endpoint,
    string         HttpMethod,
    string         PayloadJson,
    string?        LocalRefId,
    DateTimeOffset CreatedAt,
    int            RetryCount,
    string?        LastError);

public sealed record ConflictInfo(
    PendingSyncOperation Operation,
    string               ServerResponseBody);

public enum ConflictResolution { ApplyLocal, KeepServer, Defer }
