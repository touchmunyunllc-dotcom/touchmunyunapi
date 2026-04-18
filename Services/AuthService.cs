using ECommerce.Models;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Dapper;
using BCrypt.Net;
using System.Security.Cryptography;
using Serilog;

namespace ECommerce.Services;

public class AuthService : IAuthService
{
  private readonly IDbConnection _connection;
  private readonly IConfiguration _configuration;
  private readonly IRedisService _redisService;
  private readonly IOtpService _otpService;
  private readonly IEmailService _emailService;
  private readonly ISMSService _smsService;
  private readonly byte[] _refreshTokenKey;
  private readonly TimeSpan _refreshTokenTtl;

  public AuthService(
      IDbConnection connection,
      IConfiguration configuration,
      IRedisService redisService,
      IOtpService otpService,
      IEmailService emailService,
      ISMSService smsService)
  {
    _connection = connection;
    _configuration = configuration;
    _redisService = redisService;
    _otpService = otpService;
    _emailService = emailService;
    _smsService = smsService;

    var refreshKey = _configuration["Jwt:RefreshTokenKey"] ?? _configuration["Jwt:Key"] ?? string.Empty;
    if (string.IsNullOrWhiteSpace(refreshKey))
    {
      throw new InvalidOperationException("Jwt:RefreshTokenKey or Jwt:Key must be configured.");
    }

    _refreshTokenKey = Encoding.UTF8.GetBytes(refreshKey);
    var refreshDays = int.TryParse(_configuration["Jwt:RefreshTokenDays"], out var days) ? days : 30;
    _refreshTokenTtl = TimeSpan.FromDays(refreshDays);
  }

  public async Task<(bool Success, User? User, string? ErrorMessage)> RegisterAsync(
      string email, string password, string name, string? phoneNumber = null, string? clientIp = null)
  {
    // Rate limiting: Check if too many registration attempts for this email
    var emailRateLimitKey = $"registration_rate_limit:{email.ToLower().Trim()}";
    var emailRequestCountStr = await _redisService.GetAsync(emailRateLimitKey);
    var emailRequestCount = int.TryParse(emailRequestCountStr, out var count) ? count : 0;

    // Allow maximum 3 registration attempts per email per hour
    const int MAX_REQUESTS_PER_EMAIL = 3;
    if (emailRequestCount >= MAX_REQUESTS_PER_EMAIL)
    {
      Log.Warning("[SECURITY] Registration rate limit hit for email {Email}", email);
      return (false, null, "Too many registration attempts. Please try again later.");
    }

    // IP-based rate limiting (if IP provided)
    if (!string.IsNullOrEmpty(clientIp))
    {
      var ipRateLimitKey = $"registration_rate_limit_ip:{clientIp}";
      var ipRequestCountStr = await _redisService.GetAsync(ipRateLimitKey);
      var ipRequestCount = int.TryParse(ipRequestCountStr, out var ipCount) ? ipCount : 0;

      // Allow maximum 5 registration attempts per IP per hour
      const int MAX_REQUESTS_PER_IP = 5;
      if (ipRequestCount >= MAX_REQUESTS_PER_IP)
      {
        Log.Warning("[SECURITY] Registration rate limit hit for IP {ClientIp}", clientIp);
        return (false, null, "Too many registration attempts from this IP. Please try again later.");
      }

      // Increment IP rate limit counter
      await _redisService.SetAsync(ipRateLimitKey, (ipRequestCount + 1).ToString(), TimeSpan.FromHours(1));
    }

    var existingUser = await GetUserByEmailAsync(email);
    if (existingUser != null)
    {
      return (false, null, "Email already exists");
    }

        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = email.ToLower().Trim(),
            Name = name.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = UserRole.Customer,
            PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        // Insert user with explicit role string conversion
        await _connection.ExecuteAsync(@"
            INSERT INTO users (id, email, name, password, role, phone_number, created_at)
            VALUES (@Id, @Email, @Name, @PasswordHash, @Role, @PhoneNumber, @CreatedAt)",
            new { 
                Id = user.Id, 
                Email = user.Email, 
                Name = user.Name, 
                PasswordHash = user.PasswordHash, 
                Role = user.Role.ToString(), // Convert enum to string: "Customer" or "Admin"
                PhoneNumber = user.PhoneNumber,
                CreatedAt = user.CreatedAt 
            });

        // Increment email rate limit counter (only after successful registration)
        await _redisService.SetAsync(emailRateLimitKey, (emailRequestCount + 1).ToString(), TimeSpan.FromHours(1));

        // Send welcome email (don't send password - security best practice)
        try
        {
            await _emailService.SendWelcomeEmailAsync(user.Email, user.Name);
        }
        catch
        {
            // Log error but don't fail registration if email fails
            // Email sending failures should not prevent user registration
        }

        return (true, user, null);
  }

  public async Task<(bool Success, User? User, string? Token, string? ErrorMessage)> LoginAsync(
      string email, string password)
  {
    // Brute-force protection: track failed login attempts per email (OWASP A07)
    var loginAttemptKey = $"login_attempts:{email.ToLower()}";
    var attemptCountStr = await _redisService.GetAsync<string>(loginAttemptKey);
    var attemptCount = int.TryParse(attemptCountStr, out var count) ? count : 0;

    if (attemptCount >= 5)
    {
      Log.Warning("Login blocked due to too many failed attempts for {Email}", email);
      return (false, null, null, "Too many failed login attempts. Please try again in 15 minutes.");
    }

    var user = await GetUserByEmailAsync(email);
    if (user == null)
    {
      await _redisService.SetAsync(loginAttemptKey, (attemptCount + 1).ToString(), TimeSpan.FromMinutes(15));
      Log.Warning("Failed login attempt for non-existent email {Email}", email);
      return (false, null, null, "Invalid email or password");
    }

    var isPasswordValid = await VerifyPasswordAsync(password, user.PasswordHash);
    if (!isPasswordValid)
    {
      await _redisService.SetAsync(loginAttemptKey, (attemptCount + 1).ToString(), TimeSpan.FromMinutes(15));
      Log.Warning("Failed login attempt for {Email} (attempt {AttemptCount})", email, attemptCount + 1);
      return (false, null, null, "Invalid email or password");
    }

    // Clear failed attempts on successful login
    await _redisService.DeleteAsync(loginAttemptKey);

    var token = await GenerateJwtTokenAsync(user);

    // Store session in Redis
    var sessionKey = $"session:{user.Id}";
    await _redisService.SetAsync(sessionKey, token, TimeSpan.FromHours(24));

    return (true, user, token, null);
  }

  public async Task<User?> GetUserByIdAsync(Guid userId)
  {
    return await _connection.QueryFirstOrDefaultAsync<User>(
        @"SELECT 
            id AS Id,
            email AS Email,
            name AS Name,
            password AS PasswordHash,
            role AS Role,
            cancellation_count AS CancellationCount,
            order_blocked_until AS OrderBlockedUntil,
            phone_number AS PhoneNumber,
            provider AS Provider,
            provider_id AS ProviderId,
            created_at AS CreatedAt,
            updated_at AS UpdatedAt
          FROM users WHERE id = @Id",
        new { Id = userId });
  }

  public async Task<User?> GetUserByEmailAsync(string email)
  {
    return await _connection.QueryFirstOrDefaultAsync<User>(
        @"SELECT 
            id AS Id,
            email AS Email,
            name AS Name,
            password AS PasswordHash,
            role AS Role,
            cancellation_count AS CancellationCount,
            order_blocked_until AS OrderBlockedUntil,
            phone_number AS PhoneNumber,
            provider AS Provider,
            provider_id AS ProviderId,
            created_at AS CreatedAt,
            updated_at AS UpdatedAt
          FROM users WHERE email = @Email",
        new { Email = email.ToLower() });
  }

  public async Task<User?> GetUserByProviderAsync(string provider, string providerId)
  {
    return await _connection.QueryFirstOrDefaultAsync<User>(
        @"SELECT 
            id AS Id,
            email AS Email,
            name AS Name,
            password AS PasswordHash,
            role AS Role,
            cancellation_count AS CancellationCount,
            order_blocked_until AS OrderBlockedUntil,
            phone_number AS PhoneNumber,
            provider AS Provider,
            provider_id AS ProviderId,
            created_at AS CreatedAt,
            updated_at AS UpdatedAt
          FROM users WHERE provider = @Provider AND provider_id = @ProviderId",
        new { Provider = provider, ProviderId = providerId });
  }

  public async Task<(bool Success, User? User, string? Token, string? ErrorMessage)> SocialLoginAsync(
      string provider, string providerId, string email, string name)
  {
    // Check if user exists with this provider
    var existingUser = await GetUserByProviderAsync(provider, providerId);
    
    if (existingUser != null)
    {
      // User exists, generate token and return
      var token = await GenerateJwtTokenAsync(existingUser);
      var sessionKey = $"session:{existingUser.Id}";
      await _redisService.SetAsync(sessionKey, token, TimeSpan.FromHours(24));
      return (true, existingUser, token, null);
    }

    // Check if user exists with this email (link accounts)
    var userByEmail = await GetUserByEmailAsync(email);
    if (userByEmail != null)
    {
      // Link the social account to existing user
      await _connection.ExecuteAsync(
          @"UPDATE users 
            SET provider = @Provider, 
                provider_id = @ProviderId,
                updated_at = CURRENT_TIMESTAMP
            WHERE id = @Id",
          new { Provider = provider, ProviderId = providerId, Id = userByEmail.Id });
      
      userByEmail.Provider = provider;
      userByEmail.ProviderId = providerId;
      var token = await GenerateJwtTokenAsync(userByEmail);
      var sessionKey = $"session:{userByEmail.Id}";
      await _redisService.SetAsync(sessionKey, token, TimeSpan.FromHours(24));
      return (true, userByEmail, token, null);
    }

    // Create new user
    var newUser = new User
    {
      Id = Guid.NewGuid(),
      Email = email.ToLower(),
      Name = name,
      PasswordHash = "", // No password for social login
      Role = UserRole.Customer,
      Provider = provider,
      ProviderId = providerId,
      CreatedAt = DateTime.UtcNow
    };

    await _connection.ExecuteAsync(
        @"INSERT INTO users (id, email, name, password, role, provider, provider_id, created_at)
          VALUES (@Id, @Email, @Name, @PasswordHash, @Role::text, @Provider, @ProviderId, @CreatedAt)",
        new
        {
          newUser.Id,
          Email = newUser.Email,
          Name = newUser.Name,
          PasswordHash = (string?)null, // NULL for social login users
          Role = newUser.Role.ToString(),
          Provider = newUser.Provider,
          ProviderId = newUser.ProviderId,
          CreatedAt = newUser.CreatedAt
        });

    var newToken = await GenerateJwtTokenAsync(newUser);
    var newSessionKey = $"session:{newUser.Id}";
    await _redisService.SetAsync(newSessionKey, newToken, TimeSpan.FromHours(24));

    // Send welcome email
    try
    {
      await _emailService.SendWelcomeEmailAsync(newUser.Email, newUser.Name);
    }
    catch
    {
      // Don't fail registration if email fails
    }

    return (true, newUser, newToken, null);
  }

  public Task<bool> VerifyPasswordAsync(string password, string passwordHash)
  {
    try
    {
      var isValid = BCrypt.Net.BCrypt.Verify(password, passwordHash);
      return Task.FromResult(isValid);
    }
    catch
    {
      return Task.FromResult(false);
    }
  }

  public Task<string> GenerateJwtTokenAsync(User user)
  {
    var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? "");
    var issuer = _configuration["Jwt:Issuer"];
    var audience = _configuration["Jwt:Audience"];
    var expirationMinutes = int.Parse(_configuration["Jwt:ExpirationMinutes"] ?? "60");

    var claims = new[]
    {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

    var tokenDescriptor = new SecurityTokenDescriptor
    {
      Subject = new ClaimsIdentity(claims),
      Expires = DateTime.UtcNow.AddMinutes(expirationMinutes),
      Issuer = issuer,
      Audience = audience,
      SigningCredentials = new SigningCredentials(
            new SymmetricSecurityKey(key),
            SecurityAlgorithms.HmacSha256Signature)
    };

    var tokenHandler = new JwtSecurityTokenHandler();
    var token = tokenHandler.CreateToken(tokenDescriptor);
    return Task.FromResult(tokenHandler.WriteToken(token));
  }

  public async Task<string> GenerateRefreshTokenAsync(User user)
  {
    var token = GenerateSecureToken();
    var tokenHash = HashToken(token);

    var userKey = $"refresh_token_user:{user.Id}";
    var existingHash = await _redisService.GetAsync(userKey);
    if (!string.IsNullOrEmpty(existingHash))
    {
      await _redisService.DeleteAsync($"refresh_token:{existingHash}");
    }

    await _redisService.SetAsync($"refresh_token:{tokenHash}", user.Id.ToString(), _refreshTokenTtl);
    await _redisService.SetAsync(userKey, tokenHash, _refreshTokenTtl);

    return token;
  }

  public async Task<(bool Success, string? AccessToken, string? RefreshToken, string? ErrorMessage)> RotateRefreshTokenAsync(string refreshToken)
  {
    if (string.IsNullOrWhiteSpace(refreshToken))
    {
      return (false, null, null, "Missing refresh token");
    }

    var tokenHash = HashToken(refreshToken);
    var userIdString = await _redisService.GetAsync($"refresh_token:{tokenHash}");
    if (string.IsNullOrWhiteSpace(userIdString) || !Guid.TryParse(userIdString, out var userId))
    {
      return (false, null, null, "Invalid refresh token");
    }

    var userKey = $"refresh_token_user:{userId}";
    var currentHash = await _redisService.GetAsync(userKey);
    if (!string.Equals(currentHash, tokenHash, StringComparison.Ordinal))
    {
      return (false, null, null, "Refresh token has been rotated");
    }

    var user = await GetUserByIdAsync(userId);
    if (user == null)
    {
      return (false, null, null, "User not found");
    }

    await _redisService.DeleteAsync($"refresh_token:{tokenHash}");
    await _redisService.DeleteAsync(userKey);

    var newAccessToken = await GenerateJwtTokenAsync(user);
    var newRefreshToken = await GenerateRefreshTokenAsync(user);

    return (true, newAccessToken, newRefreshToken, null);
  }

  public async Task<bool> RequestPasswordResetAsync(string email, string? clientIp = null)
  {
    // Rate limiting: Check if too many requests for this email
    var emailRateLimitKey = $"password_reset_rate_limit:{email.ToLower()}";
    var emailRequestCountStr = await _redisService.GetAsync(emailRateLimitKey);
    var emailRequestCount = int.TryParse(emailRequestCountStr, out var count) ? count : 0;

    // Allow maximum 3 requests per email per hour
    const int MAX_REQUESTS_PER_EMAIL = 3;
    if (emailRequestCount >= MAX_REQUESTS_PER_EMAIL)
    {
      // Don't reveal rate limit to prevent enumeration
      // Still return true to maintain email existence privacy
      return true;
    }

    // IP-based rate limiting (if IP provided)
    if (!string.IsNullOrEmpty(clientIp))
    {
      var ipRateLimitKey = $"password_reset_rate_limit_ip:{clientIp}";
      var ipRequestCountStr = await _redisService.GetAsync(ipRateLimitKey);
      var ipRequestCount = int.TryParse(ipRequestCountStr, out var ipCount) ? ipCount : 0;

      // Allow maximum 5 requests per IP per hour
      const int MAX_REQUESTS_PER_IP = 5;
      if (ipRequestCount >= MAX_REQUESTS_PER_IP)
      {
        return true;
      }

      // Increment IP rate limit counter
      await _redisService.SetAsync(ipRateLimitKey, (ipRequestCount + 1).ToString(), TimeSpan.FromHours(1));
    }

    var user = await GetUserByEmailAsync(email);
    if (user == null)
    {
      // Don't reveal if email exists for security
      return true;
    }

    // Invalidate any existing tokens for this user (only one active token at a time)
    var userTokenKey = $"password_reset_user_token:{user.Id}";
    var existingToken = await _redisService.GetAsync(userTokenKey);
    if (!string.IsNullOrEmpty(existingToken))
    {
      // Delete the old token
      var oldTokenKey = $"password_reset_token:{existingToken}";
      await _redisService.DeleteAsync(oldTokenKey);
    }

    // Generate secure random token
    var tokenBytes = new byte[32];
    using (var rng = RandomNumberGenerator.Create())
    {
      rng.GetBytes(tokenBytes);
    }
    // Convert to base64 and make URL-safe
    var token = Convert.ToBase64String(tokenBytes)
        .Replace('+', '-')
        .Replace('/', '_')
        .Replace("=", "");

    // Store token in Redis with 1 hour expiration
    // Store both token->userId and userId->token for easy invalidation
    var tokenKey = $"password_reset_token:{token}";

    await _redisService.SetAsync(tokenKey, user.Id.ToString(), TimeSpan.FromHours(1));
    await _redisService.SetAsync(userTokenKey, token, TimeSpan.FromHours(1));

    // Increment email rate limit counter
    await _redisService.SetAsync(emailRateLimitKey, (emailRequestCount + 1).ToString(), TimeSpan.FromHours(1));

    // Get frontend URL from configuration
    var frontendUrl = _configuration["FrontendUrl"] ?? "http://localhost:3000";
    var resetLink = $"{frontendUrl}/reset-password?token={token}";

    // Send reset link via email
    await _emailService.SendPasswordResetLinkAsync(email, resetLink);

    return true;
  }

  public async Task<bool> VerifyPasswordResetAsync(string token, string newPassword)
  {
    // Get user ID from token
    var tokenKey = $"password_reset_token:{token}";
    var userIdString = await _redisService.GetAsync<string>(tokenKey);

    if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
    {
      return false;
    }

    var user = await GetUserByIdAsync(userId);
    if (user == null)
    {
      return false;
    }

    // Update password
    var newPasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
    await _connection.ExecuteAsync(
        "UPDATE users SET password = @PasswordHash, updated_at = CURRENT_TIMESTAMP WHERE id = @Id",
        new { PasswordHash = newPasswordHash, Id = user.Id });

    // Invalidate token and user token mapping
    await _redisService.DeleteAsync(tokenKey);
    var userTokenKey = $"password_reset_user_token:{user.Id}";
    await _redisService.DeleteAsync(userTokenKey);

    return true;
  }

  public async Task<bool> LogoutAsync(Guid userId)
  {
    try
    {
      var sessionKey = $"session:{userId}";
      await _redisService.DeleteAsync(sessionKey);

      var userKey = $"refresh_token_user:{userId}";
      var refreshHash = await _redisService.GetAsync(userKey);
      if (!string.IsNullOrEmpty(refreshHash))
      {
        await _redisService.DeleteAsync($"refresh_token:{refreshHash}");
        await _redisService.DeleteAsync(userKey);
      }
      return true;
    }
    catch
    {
      return false;
    }
  }

  private string GenerateSecureToken()
  {
    var tokenBytes = new byte[32];
    using var rng = RandomNumberGenerator.Create();
    rng.GetBytes(tokenBytes);
    return Convert.ToBase64String(tokenBytes)
        .Replace('+', '-')
        .Replace('/', '_')
        .Replace("=", "");
  }

  private string HashToken(string token)
  {
    using var hmac = new HMACSHA256(_refreshTokenKey);
    var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(token));
    return Convert.ToBase64String(hashBytes);
  }

  public async Task<(bool Success, User? User, string? ErrorMessage)> UpdateProfileAsync(
      Guid userId, string? name, string? email, string? phoneNumber = null)
  {
    try
    {
      var user = await GetUserByIdAsync(userId);
      if (user == null)
      {
        return (false, null, "User not found");
      }

      // Check if email is being changed and if it's already taken
      if (!string.IsNullOrEmpty(email) && email.ToLower() != user.Email.ToLower())
      {
        var existingUser = await GetUserByEmailAsync(email);
        if (existingUser != null && existingUser.Id != userId)
        {
          return (false, null, "Email is already in use");
        }
      }

      // Build update query dynamically
      var updateFields = new List<string>();
      var parameters = new DynamicParameters();
      parameters.Add("Id", userId);

      if (!string.IsNullOrEmpty(name))
      {
        updateFields.Add("name = @Name");
        parameters.Add("Name", name.Trim());
      }

      if (!string.IsNullOrEmpty(email))
      {
        updateFields.Add("email = @Email");
        parameters.Add("Email", email.ToLower().Trim());
      }

      if (phoneNumber != null)
      {
        updateFields.Add("phone_number = @PhoneNumber");
        parameters.Add("PhoneNumber", string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim());
      }

      if (updateFields.Count == 0)
      {
        return (false, null, "No fields to update");
      }

      updateFields.Add("updated_at = CURRENT_TIMESTAMP");

      var sql = $"UPDATE users SET {string.Join(", ", updateFields)} WHERE id = @Id";
      await _connection.ExecuteAsync(sql, parameters);

      // Get updated user
      var updatedUser = await GetUserByIdAsync(userId);
      return (true, updatedUser, null);
    }
    catch (Exception ex)
    {
      return (false, null, $"Failed to update profile: {ex.Message}");
    }
  }

  public async Task<(bool Success, string? ErrorMessage)> ChangePasswordAsync(
      Guid userId, string currentPassword, string newPassword)
  {
    try
    {
      var user = await GetUserByIdAsync(userId);
      if (user == null)
      {
        return (false, "User not found");
      }

      // Verify current password
      if (!VerifyPasswordAsync(currentPassword, user.PasswordHash).Result)
      {
        return (false, "Current password is incorrect");
      }

      // Validate new password
      if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
      {
        return (false, "New password must be at least 6 characters long");
      }

      // Hash new password
      var newPasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
      
      // Update password
      await _connection.ExecuteAsync(
          "UPDATE users SET password = @PasswordHash, updated_at = CURRENT_TIMESTAMP WHERE id = @Id",
          new { PasswordHash = newPasswordHash, Id = userId });

      return (true, null);
    }
    catch (Exception ex)
    {
      return (false, $"Failed to change password: {ex.Message}");
    }
  }
}
