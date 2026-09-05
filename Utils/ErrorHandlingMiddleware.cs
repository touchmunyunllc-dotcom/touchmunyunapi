using System.Net;
using System.Text.Json;
using ECommerce.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Utils;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public ErrorHandlingMiddleware(
        RequestDelegate next,
        ILogger<ErrorHandlingMiddleware> logger,
        IServiceScopeFactory scopeFactory)
    {
        _next = next;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred");
            await TryPersistImportantExceptionAsync(context, ex);
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task TryPersistImportantExceptionAsync(HttpContext context, Exception exception)
    {
        if (!ExceptionLogPolicy.IsImportant(exception))
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var exceptionLogService = scope.ServiceProvider.GetRequiredService<IExceptionLogService>();
            var statusCode = ResolveStatusCode(exception);
            await exceptionLogService.LogImportantAsync(
                exception,
                "ErrorHandlingMiddleware",
                context,
                statusCode);
        }
        catch (Exception logEx)
        {
            _logger.LogWarning(logEx, "Failed to persist important exception to database");
        }
    }

    private static int ResolveStatusCode(Exception exception)
    {
        return exception switch
        {
            BusinessException businessEx => businessEx switch
            {
                ProductNotFoundException => (int)HttpStatusCode.NotFound,
                InsufficientStockException => (int)HttpStatusCode.Conflict,
                DuplicateCouponException => (int)HttpStatusCode.Conflict,
                _ => (int)HttpStatusCode.BadRequest
            },
            UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
            ArgumentNullException or ArgumentException => (int)HttpStatusCode.BadRequest,
            KeyNotFoundException => (int)HttpStatusCode.NotFound,
            InvalidOperationException => (int)HttpStatusCode.BadRequest,
            _ => (int)HttpStatusCode.InternalServerError
        };
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var code = HttpStatusCode.InternalServerError;
        var message = "An error occurred while processing your request.";
        string? errorCode = null;

        switch (exception)
        {
            case BusinessException businessEx:
                code = businessEx switch
                {
                    ProductNotFoundException => HttpStatusCode.NotFound,
                    InsufficientStockException => HttpStatusCode.Conflict,
                    CouponNotFoundException => HttpStatusCode.NotFound,
                    CouponExpiredException or CouponUsageLimitException or MinPurchaseAmountException => HttpStatusCode.BadRequest,
                    InvalidOrderStatusException or OrderBlockedException => HttpStatusCode.BadRequest,
                    CartValidationException or AmountMismatchException => HttpStatusCode.BadRequest,
                    DuplicateCouponException => HttpStatusCode.Conflict,
                    _ => HttpStatusCode.BadRequest
                };
                message = businessEx.Message;
                errorCode = businessEx.ErrorCode;
                break;
            case UnauthorizedAccessException:
                code = HttpStatusCode.Unauthorized;
                message = "Unauthorized access";
                break;
            case ArgumentNullException:
                code = HttpStatusCode.BadRequest;
                message = "Invalid request. A required value was not provided.";
                break;
            case ArgumentException:
                code = HttpStatusCode.BadRequest;
                message = "Invalid request parameters.";
                break;
            case KeyNotFoundException:
                code = HttpStatusCode.NotFound;
                message = "The requested resource was not found.";
                break;
            case InvalidOperationException:
                code = HttpStatusCode.BadRequest;
                message = exception.Message;
                break;
        }

        var result = errorCode != null
            ? JsonSerializer.Serialize(new { message, code = errorCode })
            : JsonSerializer.Serialize(new { message });
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)code;

        return context.Response.WriteAsync(result);
    }
}

