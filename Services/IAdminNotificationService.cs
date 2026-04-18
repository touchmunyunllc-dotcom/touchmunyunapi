namespace ECommerce.Services;

public interface IAdminNotificationService
{
    Task NotifyFailedPaymentAsync(string orderId, string paymentId, decimal amount, string reason);
    Task NotifyHighVolumeOrderAsync(string orderId, decimal amount, int itemCount);
    Task NotifyLowStockAsync(Guid productId, string productName, int currentStock);
}

