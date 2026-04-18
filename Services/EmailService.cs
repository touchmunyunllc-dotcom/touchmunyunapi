using System.Net.Http.Json;
using System.Text.Json;

namespace ECommerce.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;
    private readonly HttpClient _httpClient;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger, IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.BaseAddress = new Uri("https://api.resend.com/");
        
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
            // Don't throw - email failures shouldn't break the application
        }
    }

    public async Task SendOrderConfirmationAsync(string to, string orderId, decimal total, List<OrderItemInfo>? items = null)
    {
        var itemsHtml = items != null && items.Any()
            ? string.Join("", items.Select(item => $@"
                <tr>
                    <td style=""padding: 10px; border-bottom: 1px solid #eee;"">{item.Name}</td>
                    <td style=""padding: 10px; border-bottom: 1px solid #eee; text-align: center;"">{item.Quantity}</td>
                    <td style=""padding: 10px; border-bottom: 1px solid #eee; text-align: right;"">${item.Price:F2}</td>
                </tr>"))
            : "";

        var htmlBody = $@"
            <!DOCTYPE html>
            <html>
            <head>
                <style>
                    body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                    .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                    .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }}
                    .content {{ background: #f9f9f9; padding: 30px; border-radius: 0 0 10px 10px; }}
                    .order-details {{ background: white; padding: 20px; border-radius: 5px; margin: 20px 0; }}
                    table {{ width: 100%; border-collapse: collapse; }}
                    .total {{ font-size: 20px; font-weight: bold; color: #667eea; }}
                </style>
            </head>
            <body>
                <div class=""container"">
                    <div class=""header"">
                        <h1>Order Confirmation</h1>
                    </div>
                    <div class=""content"">
                        <p>Thank you for your order!</p>
                        <div class=""order-details"">
                            <h2>Order Details</h2>
                            <p><strong>Order ID:</strong> #{orderId.Substring(0, 8)}</p>
                            <p><strong>Total Amount:</strong> ${total:F2}</p>
                            {(items != null && items.Any() ? $@"
                            <h3>Order Items</h3>
                            <table>
                                <thead>
                                    <tr style=""background: #f5f5f5;"">
                                        <th style=""padding: 10px; text-align: left;"">Item</th>
                                        <th style=""padding: 10px; text-align: center;"">Quantity</th>
                                        <th style=""padding: 10px; text-align: right;"">Price</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {itemsHtml}
                                </tbody>
                            </table>
                            " : "")}
                            <p class=""total"">Total: ${total:F2}</p>
                        </div>
                        <p>Your order is being processed and you will receive a shipping confirmation soon.</p>
                        <p>If you have any questions, please contact our support team.</p>
                    </div>
                </div>
            </body>
            </html>
        ";

        var textBody = $@"
            Thank you for your order!
            
            Order ID: {orderId}
            Total: ${total:F2}
            
            Your order is being processed and you will receive a shipping confirmation soon.
        ";

        await SendEmailAsync(to, $"Order Confirmation - Order #{orderId.Substring(0, 8)}", textBody, htmlBody);
    }

    public async Task SendPaymentReceiptAsync(string to, string orderId, string paymentId, decimal amount, DateTime paymentDate)
    {
        var htmlBody = $@"
            <!DOCTYPE html>
            <html>
            <head>
                <style>
                    body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                    .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                    .header {{ background: linear-gradient(135deg, #10b981 0%, #059669 100%); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }}
                    .content {{ background: #f9f9f9; padding: 30px; border-radius: 0 0 10px 10px; }}
                    .receipt {{ background: white; padding: 20px; border-radius: 5px; margin: 20px 0; }}
                    .amount {{ font-size: 24px; font-weight: bold; color: #10b981; }}
                </style>
            </head>
            <body>
                <div class=""container"">
                    <div class=""header"">
                        <h1>Payment Receipt</h1>
                    </div>
                    <div class=""content"">
                        <p>Thank you for your payment!</p>
                        <div class=""receipt"">
                            <h2>Payment Details</h2>
                            <p><strong>Payment ID:</strong> {paymentId}</p>
                            <p><strong>Order ID:</strong> #{orderId.Substring(0, 8)}</p>
                            <p><strong>Payment Date:</strong> {paymentDate:MMMM dd, yyyy 'at' HH:mm}</p>
                            <p class=""amount"">Amount Paid: ${amount:F2}</p>
                        </div>
                        <p>This email serves as your payment receipt. Please keep it for your records.</p>
                    </div>
                </div>
            </body>
            </html>
        ";

        var textBody = $@"
            Payment Receipt
            
            Payment ID: {paymentId}
            Order ID: {orderId}
            Amount: ${amount:F2}
            Date: {paymentDate:MMMM dd, yyyy 'at' HH:mm}
            
            This email serves as your payment receipt.
        ";

        await SendEmailAsync(to, $"Payment Receipt - Order #{orderId.Substring(0, 8)}", textBody, htmlBody);
    }

    public async Task SendOrderStatusUpdateAsync(string to, string orderId, string status, string? notes = null)
    {
        var statusMessages = new Dictionary<string, string>
        {
            { "Paid", "Your payment has been confirmed and your order is being prepared." },
            { "Packed", "Your order has been packed and is ready for shipment." },
            { "Shipped", "Your order has been shipped! Track your package using the tracking information provided." },
            { "Delivered", "Your order has been delivered! We hope you enjoy your purchase." },
            { "Cancelled", "Your order has been cancelled. If you have any questions, please contact support." }
        };

        var message = statusMessages.ContainsKey(status) ? statusMessages[status] : $"Your order status has been updated to {status}.";

        var htmlBody = $@"
            <!DOCTYPE html>
            <html>
            <head>
                <style>
                    body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                    .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                    .header {{ background: linear-gradient(135deg, #3b82f6 0%, #2563eb 100%); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }}
                    .content {{ background: #f9f9f9; padding: 30px; border-radius: 0 0 10px 10px; }}
                    .status {{ background: white; padding: 20px; border-radius: 5px; margin: 20px 0; }}
                    .status-badge {{ display: inline-block; padding: 8px 16px; background: #3b82f6; color: white; border-radius: 20px; font-weight: bold; }}
                </style>
            </head>
            <body>
                <div class=""container"">
                    <div class=""header"">
                        <h1>Order Status Update</h1>
                    </div>
                    <div class=""content"">
                        <p>Your order status has been updated.</p>
                        <div class=""status"">
                            <p><strong>Order ID:</strong> #{orderId.Substring(0, 8)}</p>
                            <p><strong>New Status:</strong> <span class=""status-badge"">{status}</span></p>
                            <p>{message}</p>
                            {(string.IsNullOrEmpty(notes) ? "" : $@"<p><strong>Notes:</strong> {notes}</p>")}
                        </div>
                    </div>
                </div>
            </body>
            </html>
        ";

        var textBody = $@"
            Order Status Update
            
            Order ID: {orderId}
            New Status: {status}
            
            {message}
            {(string.IsNullOrEmpty(notes) ? "" : $"\nNotes: {notes}")}
        ";

        await SendEmailAsync(to, $"Order Status Update - Order #{orderId.Substring(0, 8)}", textBody, htmlBody);
    }

    public async Task SendInvoiceAsync(string to, string orderId, string invoiceHtml, string? invoiceUrl = null)
    {
        var orderCode = orderId.Length > 8 ? orderId.Substring(0, 8) : orderId;
        
        var textBody = $@"
            Invoice for Order #{orderCode}
            
            Please find your invoice attached below.
            {(string.IsNullOrEmpty(invoiceUrl) ? "" : $"You can also download it from: {invoiceUrl}")}
            
            Thank you for your purchase!
        ";

        var htmlBody = $@"
            <!DOCTYPE html>
            <html>
            <head>
                <style>
                    body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                    .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                    .header {{ background: linear-gradient(135deg, #3b82f6 0%, #2563eb 100%); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }}
                    .content {{ background: #f9f9f9; padding: 30px; border-radius: 0 0 10px 10px; }}
                    .invoice-section {{ background: white; padding: 20px; border-radius: 5px; margin: 20px 0; }}
                    .button {{ display: inline-block; padding: 12px 24px; background: #3b82f6; color: white; text-decoration: none; border-radius: 5px; margin-top: 10px; }}
                </style>
            </head>
            <body>
                <div class=""container"">
                    <div class=""header"">
                        <h1>Your Invoice</h1>
                    </div>
                    <div class=""content"">
                        <p>Thank you for your purchase! Please find your invoice below.</p>
                        <div class=""invoice-section"">
                            <p><strong>Order ID:</strong> #{orderCode}</p>
                            {(string.IsNullOrEmpty(invoiceUrl) ? "" : $@"<p><a href=""{invoiceUrl}"" class=""button"">Download Invoice PDF</a></p>")}
                        </div>
                        <div style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #ddd;"">
                            <h3>Invoice Details:</h3>
                            {invoiceHtml}
                        </div>
                    </div>
                </div>
            </body>
            </html>
        ";

        await SendEmailAsync(to, $"Invoice - Order #{orderCode}", textBody, htmlBody);
    }

    public async Task SendPasswordResetLinkAsync(string to, string resetLink)
    {
        var htmlBody = $@"
            <!DOCTYPE html>
            <html>
            <head>
                <style>
                    body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                    .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                    .header {{ background: linear-gradient(135deg, #f59e0b 0%, #d97706 100%); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }}
                    .content {{ background: #f9f9f9; padding: 30px; border-radius: 0 0 10px 10px; }}
                    .button {{ display: inline-block; background: linear-gradient(135deg, #f59e0b 0%, #d97706 100%); color: white; padding: 15px 30px; text-decoration: none; border-radius: 5px; font-weight: bold; margin: 20px 0; }}
                    .button:hover {{ opacity: 0.9; }}
                    .link {{ color: #f59e0b; word-break: break-all; }}
                </style>
            </head>
            <body>
                <div class=""container"">
                    <div class=""header"">
                        <h1>Password Reset Request</h1>
                    </div>
                    <div class=""content"">
                        <p>You requested a password reset. Click the button below to reset your password:</p>
                        <div style=""text-align: center; margin: 30px 0;"">
                            <a href=""{resetLink}"" class=""button"">Reset Password</a>
                        </div>
                        <p>Or copy and paste this link into your browser:</p>
                        <p class=""link"">{resetLink}</p>
                        <p style=""margin-top: 30px; color: #666; font-size: 14px;"">This link will expire in 1 hour.</p>
                        <p style=""color: #666; font-size: 14px;""><strong>If you didn't request this, please ignore this email. Your password will remain unchanged.</strong></p>
                    </div>
                </div>
            </body>
            </html>
        ";

        var textBody = $@"
            Password Reset Request
            
            You requested a password reset. Click the link below to reset your password:
            
            {resetLink}
            
            This link will expire in 1 hour.
            
            If you didn't request this, please ignore this email. Your password will remain unchanged.
        ";

        await SendEmailAsync(to, "Password Reset Request", textBody, htmlBody);
    }

    public async Task SendWelcomeEmailAsync(string to, string name)
    {
        var frontendUrl = _configuration["FrontendUrl"] ?? "http://localhost:3000";
        var loginLink = $"{frontendUrl}/login";
        
        var htmlBody = $@"
            <!DOCTYPE html>
            <html>
            <head>
                <style>
                    body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                    .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                    .header {{ background: linear-gradient(135deg, #f59e0b 0%, #d97706 100%); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }}
                    .content {{ background: #f9f9f9; padding: 30px; border-radius: 0 0 10px 10px; }}
                    .button {{ display: inline-block; background: linear-gradient(135deg, #f59e0b 0%, #d97706 100%); color: white; padding: 15px 30px; text-decoration: none; border-radius: 5px; font-weight: bold; margin: 20px 0; }}
                    .button:hover {{ opacity: 0.9; }}
                    .info-box {{ background: white; padding: 20px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #f59e0b; }}
                </style>
            </head>
            <body>
                <div class=""container"">
                    <div class=""header"">
                        <h1>Welcome to TouchMunyun!</h1>
                    </div>
                    <div class=""content"">
                        <p>Hi {name},</p>
                        <p>Thank you for creating an account with us! We're excited to have you as part of our community.</p>
                        
                        <div class=""info-box"">
                            <p><strong>Your account has been successfully created.</strong></p>
                            <p>You can now start shopping and enjoy all the benefits of being a member!</p>
                        </div>
                        
                        <div style=""text-align: center; margin: 30px 0;"">
                            <a href=""{loginLink}"" class=""button"">Start Shopping</a>
                        </div>
                        
                        <p><strong>What's next?</strong></p>
                        <ul>
                            <li>Browse our collection of handcrafted fashion and sportswear</li>
                            <li>Add items to your cart and checkout securely</li>
                            <li>Track your orders and manage your account</li>
                        </ul>
                        
                        <p style=""margin-top: 30px; color: #666; font-size: 14px;"">
                            <strong>Security Note:</strong> For your security, we never send passwords via email. 
                            If you forget your password, you can reset it using the ""Forgot Password"" link on the login page.
                        </p>
                        
                        <p style=""color: #666; font-size: 14px;"">
                            If you have any questions, feel free to contact our support team. We're here to help!
                        </p>
                        
                        <p style=""margin-top: 30px;"">
                            Happy Shopping!<br>
                            <strong>The TouchMunyun Team</strong>
                        </p>
                    </div>
                </div>
            </body>
            </html>
        ";

        var textBody = $@"
            Welcome to TouchMunyun!
            
            Hi {name},
            
            Thank you for creating an account with us! We're excited to have you as part of our community.
            
            Your account has been successfully created. You can now start shopping and enjoy all the benefits of being a member!
            
            Start shopping: {loginLink}
            
            What's next?
            - Browse our collection of handcrafted fashion and sportswear
            - Add items to your cart and checkout securely
            - Track your orders and manage your account
            
            Security Note: For your security, we never send passwords via email. If you forget your password, you can reset it using the ""Forgot Password"" link on the login page.
            
            If you have any questions, feel free to contact our support team. We're here to help!
            
            Happy Shopping!
            The TouchMunyun Team
        ";

        await SendEmailAsync(to, "Welcome to TouchMunyun!", textBody, htmlBody);
    }
}
