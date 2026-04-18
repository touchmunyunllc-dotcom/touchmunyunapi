namespace ECommerce.Services;

public interface ISMSService
{
    Task SendSMSAsync(string to, string message);
    Task SendOTPAsync(string to, string otp);
    Task SendOrderConfirmationAsync(string to, string orderId, decimal total);
    Task SendOrderStatusUpdateAsync(string to, string orderId, string status);
    Task SendOrderNotificationAsync(string to, string orderId);
}
