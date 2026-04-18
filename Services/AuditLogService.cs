using Microsoft.Extensions.Logging;

namespace ECommerce.Services;

public interface IAuditLogService
{
    void LogAdminAction(Guid adminUserId, string action, string entityType, string? entityId = null, object? details = null);
}

public class AuditLogService : IAuditLogService
{
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(ILogger<AuditLogService> logger)
    {
        _logger = logger;
    }

    public void LogAdminAction(Guid adminUserId, string action, string entityType, string? entityId = null, object? details = null)
    {
        _logger.LogInformation(
            "AUDIT: Admin {AdminUserId} performed {Action} on {EntityType} {EntityId} {@Details}",
            adminUserId, action, entityType, entityId ?? "N/A", details);
    }
}
