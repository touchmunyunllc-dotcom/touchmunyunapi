using ECommerce.DTOs;
using ECommerce.Models;
using ECommerce.Services;
using ECommerce.Utils;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using System.Threading.RateLimiting;

namespace ECommerce.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private const string AccessTokenCookie = "authToken";
    private const string RefreshTokenCookie = "refreshToken";
    private readonly IAuthService _authService;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<RequestPasswordResetRequest> _passwordResetValidator;
    private readonly IValidator<VerifyPasswordResetRequest> _verifyPasswordResetValidator;
    private readonly IConfiguration _configuration;
    private readonly IRecaptchaService _recaptchaService;

    public AuthController(
        IAuthService authService,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator,
        IValidator<RequestPasswordResetRequest> passwordResetValidator,
        IValidator<VerifyPasswordResetRequest> verifyPasswordResetValidator,
        IConfiguration configuration,
        IRecaptchaService recaptchaService)
    {
        _authService = authService;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _passwordResetValidator = passwordResetValidator;
        _verifyPasswordResetValidator = verifyPasswordResetValidator;
        _configuration = configuration;
        _recaptchaService = recaptchaService;
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var validationResult = await _registerValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.Errors);
        }

        // Verify reCAPTCHA
        var clientIp = ClientIpHelper.GetClientIpAddress(HttpContext);

        var captchaValid = await _recaptchaService.VerifyAsync(request.CaptchaToken, "register", clientIp);
        if (!captchaValid)
        {
            return BadRequest(new { message = "CAPTCHA verification failed. Please try again." });
        }

        var (success, user, errorMessage) = await _authService.RegisterAsync(
            request.Email, request.Password, request.Name, request.PhoneNumber, clientIp);

        if (!success || user == null)
        {
            return BadRequest(new { message = errorMessage ?? "Registration failed" });
        }

        var token = await _authService.GenerateJwtTokenAsync(user);
        var refreshToken = await _authService.GenerateRefreshTokenAsync(user);

        SetAuthCookies(token, refreshToken);

        return Ok(new AuthResponse
        {
            Token = token,
            User = new UserResponse
            {
                Id = user.Id.ToString(),
                Email = user.Email,
                Name = user.Name,
                Role = user.Role.ToString().ToLower(),
                PhoneNumber = user.PhoneNumber
            }
        });
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var validationResult = await _loginValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.Errors);
        }

        var (success, user, token, errorMessage) = await _authService.LoginAsync(
            request.Email, request.Password);

        if (!success || user == null || token == null)
        {
            return Unauthorized(new { message = errorMessage ?? "Invalid credentials" });
        }

        var refreshToken = await _authService.GenerateRefreshTokenAsync(user);
        SetAuthCookies(token, refreshToken);

        return Ok(new AuthResponse
        {
            Token = token,
            User = new UserResponse
            {
                Id = user.Id.ToString(),
                Email = user.Email,
                Name = user.Name,
                Role = user.Role.ToString().ToLower(),
                PhoneNumber = user.PhoneNumber
            }
        });
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserResponse), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null || !Guid.TryParse(userId, out var userIdGuid))
        {
            return Unauthorized();
        }

        var user = await _authService.GetUserByIdAsync(userIdGuid);
        if (user == null)
        {
            return NotFound();
        }

        return Ok(new UserResponse
        {
            Id = user.Id.ToString(),
            Email = user.Email,
            Name = user.Name,
            Role = user.Role.ToString().ToLower(),
            PhoneNumber = user.PhoneNumber
        });
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(200)]
    public async Task<IActionResult> Logout()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId != null && Guid.TryParse(userId, out var userIdGuid))
        {
            await _authService.LogoutAsync(userIdGuid);
        }

        ClearAuthCookies();

        return Ok(new { message = "Logged out successfully" });
    }

    [HttpPost("refresh")]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Refresh()
    {
        var refreshToken = Request.Cookies[RefreshTokenCookie];
        var result = await _authService.RotateRefreshTokenAsync(refreshToken ?? string.Empty);

        if (!result.Success || result.AccessToken == null || result.RefreshToken == null)
        {
            ClearAuthCookies();
            return Unauthorized(new { message = result.ErrorMessage ?? "Invalid refresh token" });
        }

        SetAuthCookies(result.AccessToken, result.RefreshToken);
        return Ok(new { token = result.AccessToken });
    }

    [HttpPut("profile")]
    [Authorize]
    [ProducesResponseType(typeof(UserResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null || !Guid.TryParse(userId, out var userIdGuid))
        {
            return Unauthorized();
        }

        var (success, user, errorMessage) = await _authService.UpdateProfileAsync(
            userIdGuid, request.Name, request.Email, request.PhoneNumber);

        if (!success || user == null)
        {
            return BadRequest(new { message = errorMessage ?? "Failed to update profile" });
        }

        return Ok(new UserResponse
        {
            Id = user.Id.ToString(),
            Email = user.Email,
            Name = user.Name,
            Role = user.Role.ToString().ToLower(),
            PhoneNumber = user.PhoneNumber
        });
    }

    [HttpPut("password")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null || !Guid.TryParse(userId, out var userIdGuid))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
        {
            return BadRequest(new { message = "Current password is required" });
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new { message = "New password is required" });
        }

        if (request.NewPassword.Length < 6)
        {
            return BadRequest(new { message = "New password must be at least 6 characters long" });
        }

        var (success, errorMessage) = await _authService.ChangePasswordAsync(
            userIdGuid, request.CurrentPassword, request.NewPassword);

        if (!success)
        {
            return BadRequest(new { message = errorMessage ?? "Failed to change password" });
        }

        return Ok(new { message = "Password changed successfully" });
    }

    [HttpPost("password-reset/request")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> RequestPasswordReset([FromBody] RequestPasswordResetRequest request)
    {
        var validationResult = await _passwordResetValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.Errors);
        }

        var clientIp = ClientIpHelper.GetClientIpAddress(HttpContext);

        await _authService.RequestPasswordResetAsync(request.Email, clientIp);

        // Always return success message for security (don't reveal if email exists)
        return Ok(new { message = "If an account with that email exists, a password reset link has been sent." });
    }

    [HttpPost("password-reset/verify")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> VerifyPasswordReset([FromBody] VerifyPasswordResetRequest request)
    {
        var validationResult = await _verifyPasswordResetValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.Errors);
        }

        var success = await _authService.VerifyPasswordResetAsync(
            request.Token, request.NewPassword);

        if (!success)
        {
            return BadRequest(new { message = "Invalid or expired reset token" });
        }

        return Ok(new { message = "Password has been reset successfully" });
    }

    [HttpGet("external/{provider}")]
    public async Task<IActionResult> ExternalLogin(string provider, [FromServices] IAuthenticationSchemeProvider schemeProvider)
    {
        var scheme = await schemeProvider.GetSchemeAsync(provider);
        if (scheme == null)
        {
            return BadRequest(new { error = $"Social login with '{provider}' is not configured. Please set the OAuth credentials in appsettings." });
        }

        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Auth", new { provider }, Request.Scheme);
        var properties = new Microsoft.AspNetCore.Authentication.AuthenticationProperties { RedirectUri = redirectUrl };
        return Challenge(properties, provider);
    }

    [HttpGet("external/{provider}/callback")]
    public async Task<IActionResult> ExternalLoginCallback(string provider)
    {
        var result = await HttpContext.AuthenticateAsync("Cookies");
        if (!result.Succeeded)
        {
            return Redirect($"/login?error=external_login_failed");
        }

        var claims = result.Principal?.Claims.ToList();
        if (claims == null || !claims.Any())
        {
            return Redirect($"/login?error=external_login_failed");
        }

        // Extract user information from claims
        var providerId = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        var email = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        var name = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value 
                  ?? claims.FirstOrDefault(c => c.Type == "name")?.Value
                  ?? email?.Split('@')[0] ?? "User";

        if (string.IsNullOrEmpty(providerId) || string.IsNullOrEmpty(email))
        {
            return Redirect($"/login?error=external_login_failed");
        }

        // Normalize provider name
        var normalizedProvider = provider switch
        {
            "Google" => "Google",
            _ => provider
        };

        // Create or get user via social login
        var (success, user, token, errorMessage) = await _authService.SocialLoginAsync(
            normalizedProvider, providerId, email, name);

        if (!success || user == null || token == null)
        {
            return Redirect($"/login?error={Uri.EscapeDataString(errorMessage ?? "external_login_failed")}");
        }

        // Sign out the cookie authentication
        await HttpContext.SignOutAsync("Cookies");

        var refreshToken = await _authService.GenerateRefreshTokenAsync(user);
        SetAuthCookies(token, refreshToken);

        // Redirect to frontend without token (cookie-based auth)
        var frontendUrl = Request.Headers["Origin"].ToString() 
                         ?? _configuration["FrontendUrl"] 
                         ?? "http://localhost:3000";
        return Redirect($"{frontendUrl}/auth/callback");
    }

    private void SetAuthCookies(string accessToken, string refreshToken)
    {
        var accessOptions = BuildCookieOptions();
        accessOptions.Expires = DateTimeOffset.UtcNow.AddMinutes(GetAccessTokenMinutes());
        Response.Cookies.Append(AccessTokenCookie, accessToken, accessOptions);

        var refreshOptions = BuildCookieOptions();
        refreshOptions.Expires = DateTimeOffset.UtcNow.AddDays(GetRefreshTokenDays());
        Response.Cookies.Append(RefreshTokenCookie, refreshToken, refreshOptions);
    }

    private void ClearAuthCookies()
    {
        var options = BuildCookieOptions();
        Response.Cookies.Delete(AccessTokenCookie, options);
        Response.Cookies.Delete(RefreshTokenCookie, options);
    }

    private CookieOptions BuildCookieOptions()
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = ResolveSameSiteMode(),
            Path = "/"
        };
    }

    private SameSiteMode ResolveSameSiteMode()
    {
        var configured = _configuration["AuthCookies:SameSite"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            return SameSiteMode.Lax;
        }

        return configured.Trim().ToLowerInvariant() switch
        {
            "none" => SameSiteMode.None,
            "strict" => SameSiteMode.Strict,
            _ => SameSiteMode.Lax
        };
    }

    private int GetAccessTokenMinutes()
    {
        return int.TryParse(_configuration["Jwt:ExpirationMinutes"], out var minutes) ? minutes : 60;
    }

    private int GetRefreshTokenDays()
    {
        return int.TryParse(_configuration["Jwt:RefreshTokenDays"], out var days) ? days : 30;
    }
}
