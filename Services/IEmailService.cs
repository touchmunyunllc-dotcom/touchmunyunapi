namespace ECommerce.Services;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body, string? htmlBody = null);
    Task SendOrderConfirmationAsync(
        string to,
        string orderCode,
        decimal total,
        List<OrderItemInfo>? items = null,
        OrderConfirmationExtras? extras = null);
    Task SendPaymentReceiptAsync(string to, string orderId, string paymentId, decimal amount, DateTime paymentDate);
    Task SendOrderStatusUpdateAsync(string to, string orderId, string status, string? notes = null);
    Task SendInvoiceAsync(string to, string orderId, string invoiceHtml, string? invoiceUrl = null);
    Task SendPasswordResetLinkAsync(string to, string resetLink);
    Task SendWelcomeEmailAsync(string to, string name);

    Task SendTrackingUpdateAsync(string to, string orderCode, string trackingNumber, string? trackingUrl = null);

    Task SendPaymentFailedCustomerAsync(string to, string orderCode);
}

public record OrderItemInfo(string Name, int Quantity, decimal Price);

public record OrderConfirmationExtras(
    string? CustomerName = null,
    string? ShippingAddress = null,
    string? PaymentMethod = null);
