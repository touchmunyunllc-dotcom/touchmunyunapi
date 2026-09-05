using ECommerce.Utils;
using Xunit;

namespace ECommerce.Api.Tests;

public class ExceptionLogPolicyTests
{
    [Fact]
    public void IsImportant_ReturnsFalse_ForBusinessExceptions()
    {
        Assert.False(ExceptionLogPolicy.IsImportant(new ProductNotFoundException("abc")));
        Assert.False(ExceptionLogPolicy.IsImportant(new InsufficientStockException("Shirt", 0)));
        Assert.False(ExceptionLogPolicy.IsImportant(new CartValidationException("Invalid cart")));
    }

    [Fact]
    public void IsImportant_ReturnsFalse_ForExpectedClientErrors()
    {
        Assert.False(ExceptionLogPolicy.IsImportant(new UnauthorizedAccessException()));
        Assert.False(ExceptionLogPolicy.IsImportant(new ArgumentException("bad input")));
        Assert.False(ExceptionLogPolicy.IsImportant(new KeyNotFoundException()));
        Assert.False(ExceptionLogPolicy.IsImportant(new InvalidOperationException("bad state")));
    }

    [Fact]
    public void IsImportant_ReturnsFalse_ForCancelledRequests()
    {
        Assert.False(ExceptionLogPolicy.IsImportant(new OperationCanceledException()));
        Assert.False(ExceptionLogPolicy.IsImportant(new TaskCanceledException()));
        Assert.False(ExceptionLogPolicy.IsImportant(
            new InvalidOperationException("request aborted", new OperationCanceledException())));
    }

    [Fact]
    public void IsImportant_ReturnsTrue_ForUnexpectedSystemFailures()
    {
        Assert.True(ExceptionLogPolicy.IsImportant(new Exception("database unavailable")));
        Assert.True(ExceptionLogPolicy.IsImportant(new NullReferenceException()));
    }
}
