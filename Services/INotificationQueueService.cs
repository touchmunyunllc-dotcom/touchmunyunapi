namespace ECommerce.Services;

public interface INotificationQueueService
{
    Task EnqueueOrderStatusNotificationAsync(OrderStatusNotificationRequest request);
    Task<OrderStatusNotificationRequest?> DequeueAsync(CancellationToken cancellationToken);
}

public record OrderStatusNotificationRequest(
    Guid OrderId,
    string OrderCode,
    string UserEmail,
    string? UserPhone,
    string Status,
    string? TrackingNumber = null,
    string? TrackingUrl = null,
    string? Notes = null);

