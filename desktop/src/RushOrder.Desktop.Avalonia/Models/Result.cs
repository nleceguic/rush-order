namespace RushOrder.Desktop.Avalonia.Models;

public readonly struct Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public Exception? Error { get; }

    private Result(bool isSuccess, T? value, Exception? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<T> Ok(T value) => new(true, value, null);
    public static Result<T> Fail(Exception error) => new(false, default, error);
}
