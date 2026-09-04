using RushOrder.Desktop.Avalonia.Models;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.Models;

public class ResultTests
{
    [Fact]
    public void Ok_carries_the_value_and_no_error()
    {
        var result = Result<int>.Ok(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Fail_carries_the_exception_and_no_value()
    {
        var ex = new InvalidOperationException("boom");
        var result = Result<int>.Fail(ex);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, result.Value);
        Assert.Same(ex, result.Error);
    }
}
