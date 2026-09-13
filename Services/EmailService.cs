using System.Net;
using System.Net.Http.Json;
using ECommerce.Utils;

namespace ECommerce.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;
    private readonly HttpClient _httpClient;
    private readonly EmailBrandOptions _brand;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger, IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.BaseAddress = new Uri("https://api.resend.com/");
        _brand = EmailBrandOptions.FromConfiguration(configuration);

        var apiKey = _configuration["Email:ResendApiKey"];
        if (!string.IsNullOrEmpty(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        }
    }

    public async Task SendEmailAsync(string to, string subject, string body, string? htmlBody = null)
    {
        try
        {
            var apiKey = _configuration["Email:ResendApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("Resend API key not configured. Email would be sent to {To} with subject: {Subject}", to, subject);
                return;
            }

            var requestBody = new
            {
                from = $"{_configuration["Email:FromName"]} <{_configuration["Email:FromEmail"]}>",
                to = new[] { to },
                subject = subject,
                text = body,
                html = htmlBody ?? body.Replace("\n", "<br>")
            };

            var response = await _httpClient.PostAsJsonAsync("/emails", requestBody);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Email sent successfully to {To} with subject: {Subject}", to, subject);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to send email to {To}. Status: {Status}, Error: {Error}",
                    to, response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email to {To}", to);
        }
    }

    public async Task SendOrderConfirmationAsync(
        string to,
        string orderCode,
        decimal total,
        List<OrderItemInfo>? items = null,
        OrderConfirmationExtras? extras = null)
    {
        extras ??= new OrderConfirmationExtras();
        var codeDisplay = OrderCodeRules.Display(orderCode);
        var greeting = string.IsNullOrWhiteSpace(extras.CustomerName)
            ? "Thank you for your order!"
            : $"Hi {extras.CustomerName.Trim()}, thank you for your order!";
        var ordersUrl = $"{_brand.FrontendUrl}/orders";

        var cardParts = new List<string>
        {
            $@"<p style=""margin:0 0 8px;""><strong>Order code:</strong> {EmailTemplates.Encode(codeDisplay)}</p>",
        };

        if (!string.IsNullOrWhiteSpace(extras.PaymentMethod))
        {
            cardParts.Add($@"<p style=""margin:0 0 8px;""><strong>Payment:</strong> {EmailTemplates.Encode(extras.PaymentMethod)}</p>");
        }

        if (!string.IsNullOrWhiteSpace(extras.ShippingAddress))
        {
            cardParts.Add(
                $@"<p style=""margin:0 0 8px;""><strong>Ship to:</strong><br/>{EmailTemplates.Encode(extras.ShippingAddress).Replace("\n", "<br/>")}</p>");
        }

        if (items != null && items.Any())
        {
            var itemRows = string.Join("", items.Select(i =>
                EmailTemplates.OrderItemRow(i.Name, i.Quantity, i.Price, i.Quantity * i.Price)));
            cardParts.Add($@"
<h3 style=""margin:20px 0 8px;font-size:16px;"">Items</h3>
<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""border-collapse:collapse;"">
  <thead>{EmailTemplates.OrderItemsTableHeader()}</thead>
  <tbody>{itemRows}</tbody>
</table>");
        }

        cardParts.Add($@"<p style=""margin:16px 0 0;font-size:20px;font-weight:bold;color:#dc2626;"">Order total: ${total:F2}</p>");

        var content = EmailTemplates.Paragraph(greeting)
            + EmailTemplates.Card(string.Join("", cardParts))
            + EmailTemplates.Paragraph("We're preparing your order. You'll get another email when it ships.");

        var htmlBody = EmailTemplates.Layout(
            _brand,
            $"Order {codeDisplay}",
            content,
            ordersUrl,
            "View your orders");

        var itemsText = items != null && items.Any()
            ? string.Join("\n", items.Select(i => $"  - {i.Name} x{i.Quantity} @ ${i.Price:F2} = ${i.Quantity * i.Price:F2}"))
            : "";

        var textBody = $"""
            {greeting}

            Order code: {codeDisplay}
            {(string.IsNullOrWhiteSpace(extras.PaymentMethod) ? "" : $"Payment: {extras.PaymentMethod}\n")}
            {(string.IsNullOrWhiteSpace(extras.ShippingAddress) ? "" : $"Ship to:\n{extras.ShippingAddress}\n")}
            {(string.IsNullOrEmpty(itemsText) ? "" : $"Items:\n{itemsText}\n")}
            Order total: ${total:F2}

            View your orders: {ordersUrl}

            We're preparing your order. You'll get another email when it ships.
            """;

        await SendEmailAsync(to, $"Order Confirmation — {codeDisplay}", textBody, htmlBody);
    }

    public async Task SendPaymentReceiptAsync(string to, string orderCode, string paymentId, decimal amount, DateTime paymentDate)
    {
        var code = OrderCodeRules.Display(orderCode);
        var card = EmailTemplates.DataTable(
            EmailTemplates.DataRow("Payment ID", EmailTemplates.Encode(paymentId))
            + EmailTemplates.DataRow("Order code", EmailTemplates.Encode(code), shaded: true)
            + EmailTemplates.DataRow("Payment date", EmailTemplates.Encode($"{paymentDate:MMMM dd, yyyy 'at' HH:mm} UTC"))
            + EmailTemplates.DataRow("Amount paid", $@"<strong style=""color:#dc2626;font-size:18px;"">${amount:F2}</strong>", shaded: true));

        var content = EmailTemplates.Paragraph("Thank you for your payment!")
            + EmailTemplates.Card(card)
            + EmailTemplates.Paragraph("This email serves as your payment receipt. Please keep it for your records.");

        var htmlBody = EmailTemplates.Layout(_brand, "Payment receipt", content);

        var textBody = $"""
            Payment Receipt

            Payment ID: {paymentId}
            Order code: {code}
            Amount: ${amount:F2}
            Date: {paymentDate:MMMM dd, yyyy 'at' HH:mm} UTC

            This email serves as your payment receipt.
            """;

        await SendEmailAsync(to, $"Payment Receipt — {code}", textBody, htmlBody);
    }

    public async Task SendOrderStatusUpdateAsync(string to, string orderCode, string status, string? notes = null)
    {
        var statusMessages = new Dictionary<string, string>
        {
            { "Paid", "Your payment has been confirmed and your order is being prepared." },
            { "Packed", "Your order has been packed and is ready for shipment." },
            { "Shipped", "Your order has been shipped! Track your package using the tracking information provided." },
            { "Delivered", "Your order has been delivered! We hope you enjoy your purchase." },
            { "Cancelled", "Your order has been cancelled. If you have any questions, please contact support." }
        };

        var message = statusMessages.TryGetValue(status, out var msg)
            ? msg
            : $"Your order status has been updated to {status}.";

        var code = OrderCodeRules.Display(orderCode);
        var notesBlock = string.IsNullOrEmpty(notes)
            ? ""
            : EmailTemplates.DataRow("Notes", EmailTemplates.Encode(notes), shaded: true);

        var card = EmailTemplates.DataTable(
            EmailTemplates.DataRow("Order code", EmailTemplates.Encode(code))
            + EmailTemplates.DataRow("Status", EmailTemplates.StatusBadge(status), shaded: true)
            + notesBlock)
            + EmailTemplates.Paragraph(message);

        var content = EmailTemplates.Paragraph("Your order status has been updated.")
            + EmailTemplates.Card(card);

        var htmlBody = EmailTemplates.Layout(
            _brand,
            "Order status update",
            content,
            $"{_brand.FrontendUrl}/orders",
            "View your orders");

        var textBody = $"""
            Order Status Update

            Order code: {code}
            New Status: {status}

            {message}
            {(string.IsNullOrEmpty(notes) ? "" : $"\nNotes: {notes}")}
            """;

        await SendEmailAsync(to, $"Order Status Update — {code}", textBody, htmlBody);
    }

    public async Task SendInvoiceAsync(string to, string orderCode, string invoiceHtml, string? invoiceUrl = null)
    {
        var code = OrderCodeRules.Display(orderCode);

        var downloadBlock = string.IsNullOrEmpty(invoiceUrl)
            ? ""
            : $@"<p style=""margin:12px 0 0;""><a href=""{WebUtility.HtmlEncode(invoiceUrl)}"" style=""color:#dc2626;font-weight:bold;"">Download invoice PDF</a></p>";

        var card = EmailTemplates.Card(
            $@"<p style=""margin:0;""><strong>Order code:</strong> {EmailTemplates.Encode(code)}</p>{downloadBlock}");

        var content = EmailTemplates.Paragraph("Thank you for your purchase! Your invoice details are below.")
            + card
            + $@"<div style=""margin-top:24px;padding-top:16px;border-top:1px solid #e5e7eb;""><h3 style=""margin:0 0 12px;font-size:16px;"">Invoice</h3>{invoiceHtml}</div>";

        var htmlBody = EmailTemplates.Layout(_brand, "Your invoice", content);

        var textBody = $"""
            Invoice for Order {code}

            Please find your invoice attached below.
            {(string.IsNullOrEmpty(invoiceUrl) ? "" : $"Download: {invoiceUrl}")}

            Thank you for your purchase!
            """;

        await SendEmailAsync(to, $"Invoice — {code}", textBody, htmlBody);
    }

    public async Task SendPasswordResetLinkAsync(string to, string resetLink)
    {
        var content = EmailTemplates.Paragraph("You requested a password reset. Use the button below to choose a new password.")
            + EmailTemplates.Paragraph("Or copy this link into your browser:")
            + $@"<p style=""word-break:break-all;font-size:13px;color:#6b7280;""><a href=""{WebUtility.HtmlEncode(resetLink)}"" style=""color:#dc2626;"">{WebUtility.HtmlEncode(resetLink)}</a></p>"
            + EmailTemplates.Paragraph("This link expires in 1 hour.")
            + @"<p style=""margin:0;font-size:13px;color:#6b7280;""><strong>If you didn't request this, ignore this email — your password will stay the same.</strong></p>";

        var htmlBody = EmailTemplates.Layout(_brand, "Password reset", content, resetLink, "Reset password");

        var textBody = $"""
            Password Reset Request

            You requested a password reset. Open this link to reset your password:

            {resetLink}

            This link will expire in 1 hour.

            If you didn't request this, please ignore this email. Your password will remain unchanged.
            """;

        await SendEmailAsync(to, "Password Reset Request", textBody, htmlBody);
    }

    public async Task SendWelcomeEmailAsync(string to, string name)
    {
        var shopUrl = $"{_brand.FrontendUrl}/products";

        var content = EmailTemplates.Paragraph($"Hi {name},")
            + EmailTemplates.Paragraph("Thank you for creating an account with Touch Munyun! We're excited to have you.")
            + EmailTemplates.Card(
                @"<p style=""margin:0;""><strong>Your account is ready.</strong> Browse our collection, customize wristbands and towels, and track orders anytime.</p>")
            + @"<p style=""margin:16px 0 8px;font-weight:bold;"">What's next?</p>
<ul style=""margin:0 0 16px;padding-left:20px;color:#374151;"">
  <li>Shop handcrafted fashion and sportswear</li>
  <li>Checkout securely with card or cash on delivery</li>
  <li>Manage orders from your account</li>
</ul>"
            + EmailTemplates.Paragraph("We never send passwords by email. Use Forgot Password on the login page if you need to reset.")
            + EmailTemplates.Paragraph("Happy shopping!")
            + @"<p style=""margin:0;font-weight:bold;"">The Touch Munyun Team</p>";

        var htmlBody = EmailTemplates.Layout(_brand, "Welcome!", content, shopUrl, "Start shopping");

        var textBody = $"""
            Welcome to Touch Munyun!

            Hi {name},

            Thank you for creating an account with us!

            Start shopping: {shopUrl}

            We never send passwords via email. Use Forgot Password on the login page if needed.

            Happy Shopping!
            The Touch Munyun Team
            """;

        await SendEmailAsync(to, "Welcome to Touch Munyun!", textBody, htmlBody);
    }

    public async Task SendTrackingUpdateAsync(
        string to,
        string orderCode,
        string trackingNumber,
        string? trackingUrl = null)
    {
        var code = OrderCodeRules.Display(orderCode);
        var trackRow = string.IsNullOrEmpty(trackingUrl)
            ? EmailTemplates.DataRow("Tracking number", EmailTemplates.Encode(trackingNumber), shaded: true)
            : EmailTemplates.DataRow(
                "Tracking",
                $@"{EmailTemplates.Encode(trackingNumber)}<br/><a href=""{WebUtility.HtmlEncode(trackingUrl)}"" style=""color:#dc2626;font-weight:bold;"">Track your package</a>",
                shaded: true);

        var card = EmailTemplates.DataTable(
            EmailTemplates.DataRow("Order code", EmailTemplates.Encode(code))
            + trackRow);

        var content = EmailTemplates.Paragraph("Your order is on the way!")
            + EmailTemplates.Card(card);

        var htmlBody = string.IsNullOrEmpty(trackingUrl)
            ? EmailTemplates.Layout(_brand, "Tracking update", content)
            : EmailTemplates.Layout(_brand, "Tracking update", content, trackingUrl, "Track package");

        var textBody = $"""
            Your order is being tracked.

            Order code: {code}
            Tracking number: {trackingNumber}
            {(string.IsNullOrEmpty(trackingUrl) ? "" : $"Track here: {trackingUrl}")}
            """;

        await SendEmailAsync(to, $"Tracking — Order {code}", textBody, htmlBody);
    }

    public async Task SendPaymentFailedCustomerAsync(string to, string orderCode)
    {
        var code = OrderCodeRules.Display(orderCode);
        var checkoutUrl = $"{_brand.FrontendUrl}/cart";

        var content = EmailTemplates.Paragraph($"We couldn't complete payment for order {code}.")
            + EmailTemplates.Paragraph("Please try checkout again or use a different payment method. If you need help, reply to this email.")
            + EmailTemplates.Card(EmailTemplates.DataTable(
                EmailTemplates.DataRow("Order code", EmailTemplates.Encode(code))));

        var htmlBody = EmailTemplates.Layout(_brand, "Payment failed", content, checkoutUrl, "Return to cart");

        var textBody = $"""
            Payment failed for order {code}.

            Please try again or contact support at {_brand.SupportEmail}.

            Cart: {checkoutUrl}
            """;

        await SendEmailAsync(to, $"Payment Failed — {code}", textBody, htmlBody);
    }
}
