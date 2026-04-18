namespace ECommerce.Services;

public class OrderNotificationBackgroundService : BackgroundService
{
    private readonly INotificationQueueService _notificationQueue;
    private readonly ILogger<OrderNotificationBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public OrderNotificationBackgroundService(
        NotificationQueueService notificationQueue,
        ILogger<OrderNotificationBackgroundService> logger,
        IServiceProvider serviceProvider)
    {
        _notificationQueue = notificationQueue;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Order Notification Background Service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var notification = await _notificationQueue.DequeueAsync(stoppingToken);
                
                if (notification != null)
                {
                    await ProcessNotificationAsync(notification, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing order status notification");
                // Continue processing other notifications
            }
        }

        _logger.LogInformation("Order Notification Background Service is stopping.");
    }

    private async Task ProcessNotificationAsync(
        OrderStatusNotificationRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing order status notification for Order {OrderCode}, Status: {Status}",
            request.OrderCode,
            request.Status);

        // Create a scope for scoped services
        using var scope = _serviceProvider.CreateScope();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var smsService = scope.ServiceProvider.GetRequiredService<ISMSService>();

        var tasks = new List<Task>();

        // Send email notification
        tasks.Add(SendEmailNotificationAsync(request, emailService, cancellationToken));

        // Send WhatsApp/SMS notification if phone number is available
        if (!string.IsNullOrEmpty(request.UserPhone))
        {
            tasks.Add(SendWhatsAppNotificationAsync(request, smsService, cancellationToken));
        }

        // Execute both notifications in parallel
        await Task.WhenAll(tasks);
    }

    private async Task SendEmailNotificationAsync(
        OrderStatusNotificationRequest request,
        IEmailService emailService,
        CancellationToken cancellationToken)
    {
        try
        {
            var statusMessages = new Dictionary<string, (string Subject, string Message)>
            {
                { "Pending", ("Order Confirmed", "Your order has been confirmed and is being processed.") },
                { "Paid", ("Payment Confirmed", "Your payment has been confirmed. We're preparing your order.") },
                { "Packed", ("Order Packed", "Great news! Your order has been packed and is ready for shipment.") },
                { "Processing", ("Order Processing", "Your order is being processed and will be shipped soon.") },
                { "Shipped", ("Order Shipped", "Your order has been shipped! Track your package using the tracking information below.") },
                { "Delivered", ("Order Delivered", "Your order has been delivered! We hope you enjoy your purchase.") },
                { "Cancelled", ("Order Cancelled", "Your order has been cancelled. Contact support if you have any questions.") }
            };

            var (subject, baseMessage) = statusMessages.ContainsKey(request.Status)
                ? statusMessages[request.Status]
                : ("Order Status Update", $"Your order status has been updated to {request.Status}.");

            var trackingInfo = "";
            if (!string.IsNullOrEmpty(request.TrackingNumber))
            {
                trackingInfo = $"\n\nTracking Number: {request.TrackingNumber}";
            }
            if (!string.IsNullOrEmpty(request.TrackingUrl))
            {
                trackingInfo += $"\nTrack Your Order: {request.TrackingUrl}";
            }

            var notes = !string.IsNullOrEmpty(request.Notes) ? $"\n\nNotes: {request.Notes}" : "";

            var message = $@"
Hello,

{baseMessage}

Order Details:
- Order Code: {request.OrderCode}
- Status: {request.Status}{trackingInfo}{notes}

Thank you for shopping with us!

Best regards,
TouchMunyun Team
";

            var htmlMessage = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }}
        .content {{ background: #f9f9f9; padding: 30px; border-radius: 0 0 10px 10px; }}
        .order-info {{ background: white; padding: 20px; border-radius: 5px; margin: 20px 0; }}
        .status-badge {{ display: inline-block; padding: 5px 15px; border-radius: 20px; font-weight: bold; }}
        .status-pending {{ background: #fef3c7; color: #92400e; }}
        .status-paid {{ background: #ddd6fe; color: #5b21b6; }}
        .status-packed {{ background: #fef3c7; color: #92400e; }}
        .status-processing {{ background: #dbeafe; color: #1e40af; }}
        .status-shipped {{ background: #d1fae5; color: #065f46; }}
        .status-delivered {{ background: #d1fae5; color: #065f46; }}
        .status-cancelled {{ background: #fee2e2; color: #991b1b; }}
        .tracking-link {{ display: inline-block; margin-top: 10px; padding: 10px 20px; background: #667eea; color: white; text-decoration: none; border-radius: 5px; }}
        .footer {{ text-align: center; margin-top: 30px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Order Status Update</h1>
        </div>
        <div class=""content"">
            <p>Hello,</p>
            <p>{baseMessage}</p>
            
            <div class=""order-info"">
                <h3>Order Details</h3>
                <p><strong>Order Code:</strong> {request.OrderCode}</p>
                <p><strong>Status:</strong> <span class=""status-badge status-{request.Status.ToLower()}"">{request.Status}</span></p>
                {(!string.IsNullOrEmpty(request.TrackingNumber) ? $"<p><strong>Tracking Number:</strong> {request.TrackingNumber}</p>" : "")}
                {(!string.IsNullOrEmpty(request.TrackingUrl) ? $"<a href=\"{request.TrackingUrl}\" class=\"tracking-link\">Track Your Order</a>" : "")}
                {(!string.IsNullOrEmpty(request.Notes) ? $"<p><strong>Notes:</strong> {request.Notes}</p>" : "")}
            </div>
            
            <p>Thank you for shopping with us!</p>
            <p>Best regards,<br>TouchMunyun Team</p>
        </div>
        <div class=""footer"">
            <p>This is an automated email. Please do not reply.</p>
        </div>
    </div>
</body>
</html>
";

            await emailService.SendOrderStatusUpdateAsync(
                request.UserEmail,
                request.OrderCode,
                request.Status,
                request.Notes);

            _logger.LogInformation("Email notification sent for Order {OrderCode}", request.OrderCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email notification for Order {OrderCode}", request.OrderCode);
            // Don't throw - continue with WhatsApp notification
        }
    }

    private async Task SendWhatsAppNotificationAsync(
        OrderStatusNotificationRequest request,
        ISMSService smsService,
        CancellationToken cancellationToken)
    {
        try
        {
            var statusMessages = new Dictionary<string, string>
            {
                { "Pending", "✅ Order Confirmed!\n\nYour order #{0} has been confirmed and is being processed. Thank you for your purchase!" },
                { "Paid", "💳 Payment Confirmed!\n\nYour payment for order #{0} has been confirmed. We're preparing your order." },
                { "Packed", "📦 Order Packed!\n\nYour order #{0} has been packed and is ready for shipment." },
                { "Processing", "⚙️ Order Processing!\n\nYour order #{0} is being processed and will be shipped soon." },
                { "Shipped", "🚚 Order Shipped!\n\nGreat news! Your order #{0} has been shipped." },
                { "Delivered", "🎉 Order Delivered!\n\nYour order #{0} has been delivered! We hope you enjoy your purchase." },
                { "Cancelled", "❌ Order Cancelled\n\nYour order #{0} has been cancelled. Contact support if you have questions." }
            };

            var messageTemplate = statusMessages.ContainsKey(request.Status)
                ? statusMessages[request.Status]
                : "📦 Order Status Update\n\nYour order #{0} status has been updated to {1}.";

            var message = string.Format(messageTemplate, request.OrderCode, request.Status);

            // Add tracking information if available
            if (!string.IsNullOrEmpty(request.TrackingNumber))
            {
                message += $"\n\n📋 Tracking Number: {request.TrackingNumber}";
            }
            if (!string.IsNullOrEmpty(request.TrackingUrl))
            {
                message += $"\n🔗 Track: {request.TrackingUrl}";
            }

            // Send via SMS service (which can handle WhatsApp if configured)
            await smsService.SendOrderStatusUpdateAsync(
                request.UserPhone!,
                request.OrderCode,
                request.Status);

            _logger.LogInformation("WhatsApp/SMS notification sent for Order {OrderCode}", request.OrderCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send WhatsApp/SMS notification for Order {OrderCode}", request.OrderCode);
            // Don't throw - notification failures shouldn't break the system
        }
    }
}

