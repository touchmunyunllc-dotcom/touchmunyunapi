namespace ECommerce.Services;

public interface IAdminNotificationService
{
    Task NotifyNewOrderAsync(AdminNewOrderAlert alert);
    Task NotifyFailedPaymentAsync(string orderCode, string paymentId, decimal amount, string reason);
    Task NotifyHighVolumeOrderAsync(string orderCode, decimal amount, int itemCount);
    Task NotifyLowStockAsync(Guid productId, string productName, int currentStock);
}

