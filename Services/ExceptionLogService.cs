using System.Data;
using System.Security.Claims;
using System.Text.Json;
using Dapper;
using ECommerce.Utils;
using Microsoft.AspNetCore.Http;

namespace ECommerce.Services;

public interface IExceptionLogService
{
    Task LogImportantAsync(
        Exception exception,
        string source,
        HttpContext? httpContext = null,
        int? statusCode = null,
        string severity = "Error",
        object? additionalData = null,
        bool forcePersist = false,
        CancellationToken cancellationToken = default);
}

public class ExceptionLogService : IExceptionLogService
{
    private const int MaxMessageLength = 2000;
    private const int MaxStackTraceLength = 12000;
    private const int MaxSourceLength = 200;
    private const int MaxRequestPathLength = 2000;

    private readonly IDbConnection _connection;
    private readonly ILogger<ExceptionLogService> _logger;

    public ExceptionLogService(IDbConnection connection, ILogger<ExceptionLogService> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    public async Task LogImportantAsync(
        Exception exception,
        string source,
        HttpContext? httpContext = null,
        int? statusCode = null,
        string severity = "Error",
        object? additionalData = null,
        bool forcePersist = false,
        CancellationToken cancellationToken = default)
    {
        if (!forcePersist && !ExceptionLogPolicy.IsImportant(exception))
        {
            return;
        }

        try
        {
            var root = GetRootException(exception);
            var additionalDataJson = additionalData == null
                ? null
                : JsonSerializer.Serialize(additionalData);

            await _connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO exception_logs (
                    id, severity, exception_type, message, stack_trace, source,
                    http_method, request_path, status_code, correlation_id, user_id, client_ip, additional_data
                ) VALUES (
                    @Id, @Severity, @ExceptionType, @Message, @StackTrace, @Source,
                    @HttpMethod, @RequestPath, @StatusCode, @CorrelationId, @UserId, @ClientIp, @AdditionalData::jsonb
                )
                """,
                new
                {
                    Id = Guid.NewGuid(),
                    Severity = Truncate(severity, 20),
                    ExceptionType = Truncate(root.GetType().FullName ?? root.GetType().Name, 500),
                    Message = Truncate(root.Message, MaxMessageLength),
                    StackTrace = Truncate(root.StackTrace, MaxStackTraceLength),
                    Source = Truncate(source, MaxSourceLength),
                    HttpMethod = httpContext?.Request.Method,
                    RequestPath = Truncate(httpContext?.Request.Path.Value, MaxRequestPathLength),
                    StatusCode = statusCode,
                    CorrelationId = httpContext?.Items["CorrelationId"]?.ToString(),
                    UserId = GetUserId(httpContext),
                    ClientIp = httpContext == null ? null : ClientIpHelper.GetClientIpAddress(httpContext),
                    AdditionalData = additionalDataJson
                },
                cancellationToken: cancellationToken));
        }
        catch (Exception logEx)
        {
            _logger.LogWarning(logEx, "Failed to persist important exception from {Source}", source);
        }
    }

    private static Exception GetRootException(Exception exception)
    {
        var current = exception;
        while (current.InnerException != null)
        {
            current = current.InnerException;
        }

        return current;
    }

    private static Guid? GetUserId(HttpContext? httpContext)
    {
        if (httpContext?.User?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var userIdValue = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? httpContext.User.FindFirst("sub")?.Value;

        return Guid.TryParse(userIdValue, out var userId) ? userId : null;
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}
