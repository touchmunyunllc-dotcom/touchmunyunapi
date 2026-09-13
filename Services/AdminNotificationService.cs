using ECommerce.Utils;

namespace ECommerce.Services;

public class AdminNotificationService : IAdminNotificationService
{
    private readonly IEmailService _emailService;
    private readonly ISMSService _smsService;
    private readonly ILogger<AdminNotificationService> _logger;
    private readonly IConfiguration _configuration;
    private readonly EmailBrandOptions _brand;

    public AdminNotificationService(
        IEmailService emailService,
        ISMSService smsService,
        ILogger<AdminNotificationService> logger,
        IConfiguration configuration)
    {
        _emailService = emailService;
        _smsService = smsService;
        _logger = logger;
        _configuration = configuration;
        _brand = EmailBrandOptions.FromConfiguration(configuration);
    }

    private async Task SendAdminEmailAsync(string subject, string textBody, string innerHtml, string? buttonUrl = null, string? buttonText = null)
    {
        var adminEmail = _configuration["Admin:Email"] ?? "TouchMunyunLLC@gmail.com";
        var html = EmailTemplates.Layout(_brand, subject, innerHtml, buttonUrl, buttonText, adminAlert: true);
        await _emailService.SendEmailAsync(adminEmail, subject, textBody, html);
    }

    public async Task NotifyNewOrderAsync(AdminNewOrderAlert alert)
    {
        if (!_configuration.GetValue("Admin:NotifyNewOrders", true))
        {
            return;
        }

        try
        {
            var adminPhone = _configuration["Admin:Phone"];
            var code = OrderCodeRules.Display(alert.OrderCode);
            var adminOrdersUrl = $"{_brand.FrontendUrl}/admin/orders";

            var customerLine = string.IsNullOrWhiteSpace(alert.CustomerName)
                ? alert.CustomerEmail ?? "Guest"
                : $"{alert.CustomerName.Trim()} ({alert.CustomerEmail ?? "no email"})";

            var subject = $"New order {code} — ${alert.TotalAmount:F2}";

            var shipBlock = string.IsNullOrWhiteSpace(alert.ShippingAddress)
                ? ""
                : EmailTemplates.DataRow(
                    "Ship to",
                    EmailTemplates.Encode(alert.ShippingAddress).Replace("\n", "<br/>"),
                    shaded: true);

            var inner = EmailTemplates.DataTable(
                EmailTemplates.DataRow("Order code", EmailTemplates.Encode(code))
                + EmailTemplates.DataRow("Status", EmailTemplates.StatusBadge(alert.Status), shaded: true)
                + EmailTemplates.DataRow("Payment", EmailTemplates.Encode(alert.PaymentMethod))
                + EmailTemplates.DataRow("Total", $@"<strong>${alert.TotalAmount:F2}</strong> ({alert.ItemCount} item(s))", shaded: true)
                + EmailTemplates.DataRow(
                    "Customer",
                    EmailTemplates.Encode(customerLine) + (alert.IsGuest ? " <em>(guest checkout)</em>" : ""))
                + shipBlock);

            var textBody = $"""
                New order {code}
                Status: {alert.Status}
                Payment: {alert.PaymentMethod}
                Total: ${alert.TotalAmount:F2} ({alert.ItemCount} items)
                Customer: {customerLine}{(alert.IsGuest ? " (guest)" : "")}
                {(string.IsNullOrWhiteSpace(alert.ShippingAddress) ? "" : $"Ship to: {alert.ShippingAddress}\n")}
                Admin: {adminOrdersUrl}
                """;

            await SendAdminEmailAsync(subject, textBody, inner, adminOrdersUrl, "Open admin orders");

            if (!string.IsNullOrEmpty(adminPhone))
            {
                await _smsService.SendSMSAsync(
                    adminPhone,
                    $"New order {code}: ${alert.TotalAmount:F2} — {alert.Status}. Check admin orders.");
            }

            _logger.LogInformation("Admin notified of new order {OrderCode}", code);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send admin new-order notification");
        }
    }

    public async Task NotifyFailedPaymentAsync(string orderCode, string paymentId, decimal amount, string reason)
    {
        try
        {
            var adminPhone = _configuration["Admin:Phone"];
            var code = OrderCodeRules.Display(orderCode);
            var subject = $"Payment failed — order {code}";

            var inner = EmailTemplates.DataTable(
                EmailTemplates.DataRow("Order code", EmailTemplates.Encode(code))
                + EmailTemplates.DataRow("Payment ID", EmailTemplates.Encode(paymentId), shaded: true)
                + EmailTemplates.DataRow("Amount", $@"${amount:F2}")
                + EmailTemplates.DataRow("Reason", EmailTemplates.Encode(reason), shaded: true))
                + EmailTemplates.Paragraph("Review this order and contact the customer if needed.");

            await SendAdminEmailAsync(
                subject,
                $"Payment failed for order {code}. Amount: ${amount:F2}. Reason: {reason}",
                inner);

            if (!string.IsNullOrEmpty(adminPhone))
            {
                await _smsService.SendSMSAsync(
                    adminPhone,
                    $"ALERT: Payment failed for order {code} - ${amount:F2}");
            }

            _logger.LogWarning("Admin notified of failed payment: Order {OrderCode}, Payment {PaymentId}", code, paymentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send admin notification for failed payment");
        }
    }

    public async Task NotifyHighVolumeOrderAsync(string orderCode, decimal amount, int itemCount)
    {
        try
        {
            var adminPhone = _configuration["Admin:Phone"];
            var highVolumeThreshold = decimal.Parse(_configuration["Admin:HighVolumeThreshold"] ?? "1000");
            var code = OrderCodeRules.Display(orderCode);

            if (amount < highVolumeThreshold)
            {
                return;
            }

            var subject = $"High volume order — {code}";
            var inner = EmailTemplates.DataTable(
                EmailTemplates.DataRow("Order code", EmailTemplates.Encode(code))
                + EmailTemplates.DataRow("Total", $@"<strong>${amount:F2}</strong>", shaded: true)
                + EmailTemplates.DataRow("Items", itemCount.ToString())
                + EmailTemplates.DataRow("Threshold", $@"${highVolumeThreshold:F2}", shaded: true))
                + EmailTemplates.Paragraph("This order exceeds your high-volume threshold. Please review handling.");

            await SendAdminEmailAsync(
                subject,
                $"High volume order {code}: ${amount:F2} with {itemCount} items",
                inner);

            if (!string.IsNullOrEmpty(adminPhone))
            {
                await _smsService.SendSMSAsync(
                    adminPhone,
                    $"High volume order {code}: ${amount:F2}");
            }

            _logger.LogInformation("Admin notified of high volume order: Order {OrderCode}, Amount {Amount}", code, amount);
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
            var lowStockThreshold = int.Parse(_configuration["Admin:LowStockThreshold"] ?? "10");

            if (currentStock > lowStockThreshold)
            {
                return;
            }

            var subject = $"Low stock — {productName}";
            var adminProductsUrl = $"{_brand.FrontendUrl}/admin/products";

            var inner = EmailTemplates.DataTable(
                EmailTemplates.DataRow("Product", EmailTemplates.Encode(productName))
                + EmailTemplates.DataRow("Product ID", EmailTemplates.Encode(productId.ToString()), shaded: true)
                + EmailTemplates.DataRow("Current stock", currentStock.ToString())
                + EmailTemplates.DataRow("Threshold", lowStockThreshold.ToString(), shaded: true))
                + EmailTemplates.Paragraph("Consider restocking this product.");

            await SendAdminEmailAsync(
                subject,
                $"Low stock alert: {productName} has only {currentStock} units remaining",
                inner,
                adminProductsUrl,
                "Manage products");

            _logger.LogWarning("Admin notified of low stock: Product {ProductId}, Stock {Stock}", productId, currentStock);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send admin notification for low stock");
        }
    }
}
