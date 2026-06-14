namespace RushOrder.API.Common;

public sealed class ApiResponse<T>
{
    public string Status { get; private init; } = "success";
    public T? Data { get; private init; }
    public string? Message { get; private init; }
    public string[]? Errors { get; private init; }
    public PaginationMeta? Meta { get; private init; }

    public static ApiResponse<T> Ok(T data, PaginationMeta? meta = null) =>
        new() { Status = "success", Data = data, Meta = meta };

    public static ApiResponse<T> Fail(string message, string[]? errors = null) =>
        new() { Status = "error", Message = message, Errors = errors };
}

public sealed class PaginationMeta
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
}
