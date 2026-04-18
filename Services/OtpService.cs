namespace ECommerce.Services;

public class OtpService : IOtpService
{
    private readonly IRedisService _redis;
    private readonly ILogger<OtpService> _logger;
    private readonly byte[] _otpKey;
    private const int OTP_LENGTH = 6;
    private const int OTP_EXPIRY_MINUTES = 10;

    public OtpService(IRedisService redis, ILogger<OtpService> logger, IConfiguration configuration)
    {
        _redis = redis;
        _logger = logger;
        var key = configuration["Otp:Key"] ?? configuration["Jwt:Key"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("OTP key is not configured. Set Otp:Key or Jwt:Key.");
        }
        _otpKey = System.Text.Encoding.UTF8.GetBytes(key);
    }

    public async Task<string> GenerateOtpAsync(string email)
    {
        var otp = GenerateNumericOtp();
        var otpHash = HashOtp(email, otp);
        
        var key = $"otp:{email}";
        await _redis.SetAsync(key, otpHash, TimeSpan.FromMinutes(OTP_EXPIRY_MINUTES));
        
        _logger.LogInformation("Generated OTP for {Email}", email);
        return otp;
    }

    public async Task<bool> ValidateOtpAsync(string email, string otp)
    {
        var key = $"otp:{email}";
        var storedOtpHash = await _redis.GetAsync(key);
        
        if (storedOtpHash == null)
        {
            return false;
        }

        var candidateHash = HashOtp(email, otp);
        var isValid = FixedTimeEquals(storedOtpHash, candidateHash);
        
        if (isValid)
        {
            await _redis.DeleteAsync(key);
        }

        return isValid;
    }

    public async Task<bool> InvalidateOtpAsync(string email)
    {
        var key = $"otp:{email}";
        return await _redis.DeleteAsync(key);
    }

    private string GenerateNumericOtp()
    {
        var value = System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, (int)Math.Pow(10, OTP_LENGTH));
        return value.ToString().PadLeft(OTP_LENGTH, '0');
    }

    private string HashOtp(string email, string otp)
    {
        var payload = $"{email}:{otp}";
        using var hmac = new System.Security.Cryptography.HMACSHA256(_otpKey);
        var hashBytes = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
        return Convert.ToBase64String(hashBytes);
    }

    private static bool FixedTimeEquals(string leftBase64, string rightBase64)
    {
        try
        {
            var left = Convert.FromBase64String(leftBase64);
            var right = Convert.FromBase64String(rightBase64);
            return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(left, right);
        }
        catch
        {
            return false;
        }
    }
}

