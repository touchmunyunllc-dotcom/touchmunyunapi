using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace ECommerce.Services;

public interface IRecaptchaService
{
    Task<bool> VerifyAsync(string? token, string action, string? remoteIp = null);
}

public class RecaptchaService : IRecaptchaService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _secretKey;
    private readonly double _minScore;
    private readonly IHostEnvironment _environment;

    public RecaptchaService(IHttpClientFactory httpClientFactory, IConfiguration configuration, IHostEnvironment environment)
    {
        _httpClientFactory = httpClientFactory;
        _secretKey = configuration["Recaptcha:SecretKey"] ?? "";
        _minScore = configuration.GetValue<double>("Recaptcha:MinScore", 0.5);
        _environment = environment;
    }

    public async Task<bool> VerifyAsync(string? token, string action, string? remoteIp = null)
    {
        // Skip verification only in development when reCAPTCHA is not configured
        if (string.IsNullOrEmpty(_secretKey))
        {
            if (_environment.IsDevelopment())
            {
                Log.Warning("[SECURITY] reCAPTCHA secret key not configured — skipping verification in development");
                return true;
            }

            Log.Error("[SECURITY] reCAPTCHA secret key not configured — verification denied");
            return false;
        }

        if (string.IsNullOrEmpty(token))
        {
            Log.Warning("[SECURITY] reCAPTCHA token missing for action {Action}", action);
            return false;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var parameters = new Dictionary<string, string>
            {
                { "secret", _secretKey },
                { "response", token }
            };

            if (!string.IsNullOrEmpty(remoteIp))
            {
                parameters.Add("remoteip", remoteIp);
            }

            var response = await client.PostAsync(
                "https://www.google.com/recaptcha/api/siteverify",
                new FormUrlEncodedContent(parameters));

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<RecaptchaResponse>(json);

            if (result == null || !result.Success)
            {
                Log.Warning("[SECURITY] reCAPTCHA verification failed for action {Action}: {Errors}",
                    action, string.Join(", ", result?.ErrorCodes ?? Array.Empty<string>()));
                return false;
            }

            // Verify the action matches
            if (!string.IsNullOrEmpty(result.Action) && result.Action != action)
            {
                Log.Warning("[SECURITY] reCAPTCHA action mismatch. Expected {Expected}, got {Actual}",
                    action, result.Action);
                return false;
            }

            // Check score (0.0 = bot, 1.0 = human)
            if (result.Score < _minScore)
            {
                Log.Warning("[SECURITY] reCAPTCHA score too low for action {Action}: {Score} (min: {MinScore})",
                    action, result.Score, _minScore);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[SECURITY] reCAPTCHA verification error for action {Action}", action);
            // Fail closed in non-development environments
            return _environment.IsDevelopment();
        }
    }
}

public class RecaptchaResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("score")]
    public double Score { get; set; }

    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("challenge_ts")]
    public DateTime ChallengeTs { get; set; }

    [JsonPropertyName("hostname")]
    public string? Hostname { get; set; }

    [JsonPropertyName("error-codes")]
    public string[]? ErrorCodes { get; set; }
}
