using ECommerce.Data;
using System.Data;
using Dapper;

namespace ECommerce.Services;

public class AdminNotificationService : IAdminNotificationService
{
    private readonly IDbConnection _connection;
    private readonly IEmailService _emailService;
    private readonly ISMSService _smsService;
    private readonly ILogger<AdminNotificationService> _logger;
    private readonly IConfiguration _configuration;

    public AdminNotificationService(
        IDbConnection connection,
        IEmailService emailService,
        ISMSService smsService,
        ILogger<AdminNotificationService> logger,
        IConfiguration configuration)
    {
        _connection = connection;
        _emailService = emailService;
        _smsService = smsService;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task NotifyFailedPaymentAsync(string orderId, string paymentId, decimal amount, string reason)
    {
        try
        {
            var adminEmail = _configuration["Admin:Email"] ?? "TouchMunyunLLC@gmail.com";
            var adminPhone = _configuration["Admin:Phone"];

            var subject = $"⚠️ Payment Failed - Order {orderId}";
            var emailBody = $@"
                <h2>Payment Failed Alert</h2>
                <p><strong>Order ID:</strong> {orderId}</p>
                <p><strong>Payment ID:</strong> {paymentId}</p>
                <p><strong>Amount:</strong> ${amount:F2}</p>
                <p><strong>Reason:</strong> {reason}</p>
                <p>Please review this order and contact the customer if necessary.</p>
            ";

            await _emailService.SendEmailAsync(
                adminEmail,
                subject,
                $"Payment failed for order {orderId}. Amount: ${amount:F2}. Reason: {reason}",
                emailBody);

            if (!string.IsNullOrEmpty(adminPhone))
            {
                await _smsService.SendSMSAsync(
                    adminPhone,
                    $"ALERT: Payment failed for order {orderId} - ${amount:F2}");
            }

            _logger.LogWarning("Admin notified of failed payment: Order {OrderId}, Payment {PaymentId}", orderId, paymentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send admin notification for failed payment");
        }
    }

    public async Task NotifyHighVolumeOrderAsync(string orderId, decimal amount, int itemCount)
    {
        try
        {
            var adminEmail = _configuration["Admin:Email"] ?? "TouchMunyunLLC@gmail.com";
            var adminPhone = _configuration["Admin:Phone"];
            var highVolumeThreshold = decimal.Parse(_configuration["Admin:HighVolumeThreshold"] ?? "1000");

            if (amount < highVolumeThreshold)
            {
                return; // Not a high-volume order
            }

            var subject = $"📦 High Volume Order - Order {orderId}";
            var emailBody = $@"
                <h2>High Volume Order Alert</h2>
                <p><strong>Order ID:</strong> {orderId}</p>
                <p><strong>Total Amount:</strong> ${amount:F2}</p>
                <p><strong>Item Count:</strong> {itemCount}</p>
                <p>This order exceeds the high-volume threshold of ${highVolumeThreshold:F2}.</p>
                <p>Please review and ensure proper handling.</p>
            ";

            await _emailService.SendEmailAsync(
                adminEmail,
                subject,
                $"High volume order {orderId}: ${amount:F2} with {itemCount} items",
                emailBody);

            if (!string.IsNullOrEmpty(adminPhone))
            {
                await _smsService.SendSMSAsync(
                    adminPhone,
                    $"High volume order {orderId}: ${amount:F2}");
            }

            _logger.LogInformation("Admin notified of high volume order: Order {OrderId}, Amount {Amount}", orderId, amount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send admin notification for high volume order");
        }
    }

    public async Task NotifyLowStockAsync(Guid productId, string productName, int currentStock)
    {
        try
        {
            var adminEmail = _configuration["Admin:Email"] ?? "TouchMunyunLLC@gmail.com";
            var lowStockThreshold = int.Parse(_configuration["Admin:LowStockThreshold"] ?? "10");

            if (currentStock > lowStockThreshold)
            {
                return; // Stock is not low
            }

            var subject = $"⚠️ Low Stock Alert - {productName}";
            var emailBody = $@"
                <h2>Low Stock Alert</h2>
                <p><strong>Product:</strong> {productName}</p>
                <p><strong>Product ID:</strong> {productId}</p>
                <p><strong>Current Stock:</strong> {currentStock}</p>
                <p><strong>Threshold:</strong> {lowStockThreshold}</p>
                <p>Please consider restocking this product.</p>
            ";

            await _emailService.SendEmailAsync(
                adminEmail,
                subject,
                $"Low stock alert: {productName} has only {currentStock} units remaining",
                emailBody);

            _logger.LogWarning("Admin notified of low stock: Product {ProductId}, Stock {Stock}", productId, currentStock);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send admin notification for low stock");
        }
    }
}

