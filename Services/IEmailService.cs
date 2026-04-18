namespace ECommerce.Services;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body, string? htmlBody = null);
    Task SendOrderConfirmationAsync(string to, string orderId, decimal total, List<OrderItemInfo>? items = null);
    Task SendPaymentReceiptAsync(string to, string orderId, string paymentId, decimal amount, DateTime paymentDate);
    Task SendOrderStatusUpdateAsync(string to, string orderId, string status, string? notes = null);
    Task SendInvoiceAsync(string to, string orderId, string invoiceHtml, string? invoiceUrl = null);
    Task SendPasswordResetLinkAsync(string to, string resetLink);
    Task SendWelcomeEmailAsync(string to, string name);
}

public record OrderItemInfo(string Name, int Quantity, decimal Price);
