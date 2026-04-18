using System.Net.Http.Json;
using System.Text.Json;

namespace ECommerce.Services;

public class SMSService : ISMSService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SMSService> _logger;
    private readonly HttpClient _httpClient;

    public SMSService(IConfiguration configuration, ILogger<SMSService> logger, IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.BaseAddress = new Uri("https://api.gupshup.io/");
    }

    public async Task SendSMSAsync(string to, string message)
    {
        try
        {
            var apiKey = _configuration["SMS:GupshupApiKey"];
            var appName = _configuration["SMS:GupshupAppName"];
            
            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(appName))
            {
                _logger.LogWarning("Gupshup API key or App Name not configured. SMS would be sent to {To} with message: {Message}", to, message);
                return;
            }

            // Gupshup API endpoint for sending SMS
            var requestBody = new
            {
                channel = "sms",
                source = appName,
                destination = to,
                message = new
                {
                    type = "text",
                    text = message
                }
            };

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("apikey", apiKey);
            _httpClient.DefaultRequestHeaders.Add("Content-Type", "application/x-www-form-urlencoded");

            // Gupshup uses form-urlencoded format
            var formData = new List<KeyValuePair<string, string>>
            {
                new("channel", "sms"),
                new("source", appName),
                new("destination", to),
                new("message", JsonSerializer.Serialize(new { type = "text", text = message }))
            };

            var response = await _httpClient.PostAsync("sm/api/v1/msg", new FormUrlEncodedContent(formData));
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("SMS sent successfully to {To}", to);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to send SMS to {To}. Status: {Status}, Error: {Error}", 
                    to, response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending SMS to {To}", to);
            // Don't throw - SMS failures shouldn't break the application
        }
    }

    public async Task SendOTPAsync(string to, string otp)
    {
        var message = $"Your verification code is {otp}. Valid for 10 minutes. Do not share this code with anyone.";
        await SendSMSAsync(to, message);
    }

    public async Task SendOrderConfirmationAsync(string to, string orderId, decimal total)
    {
        var message = $"Order Confirmed! Order #{orderId.Substring(0, 8)} - Total: ${total:F2}. Thank you for your purchase!";
        await SendSMSAsync(to, message);
    }

    public async Task SendOrderStatusUpdateAsync(string to, string orderId, string status)
    {
        var statusMessages = new Dictionary<string, string>
        {
            { "Paid", "Your payment for order #{0} has been confirmed." },
            { "Packed", "Your order #{0} has been packed and is ready for shipment." },
            { "Shipped", "Great news! Your order #{0} has been shipped. Track it using the tracking number provided." },
            { "Delivered", "Your order #{0} has been delivered! We hope you enjoy your purchase." },
            { "Cancelled", "Your order #{0} has been cancelled. Contact support if you have questions." }
        };

        var messageTemplate = statusMessages.ContainsKey(status) 
            ? statusMessages[status] 
            : "Your order #{0} status has been updated to {1}.";

        var message = string.Format(messageTemplate, orderId.Substring(0, 8), status);
        await SendSMSAsync(to, message);
    }

    public async Task SendOrderNotificationAsync(string to, string orderId)
    {
        var message = $"Your order #{orderId.Substring(0, 8)} has been confirmed and is being processed. Thank you!";
        await SendSMSAsync(to, message);
    }
}
