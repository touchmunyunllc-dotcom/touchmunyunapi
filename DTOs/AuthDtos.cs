namespace ECommerce.DTOs;

public record RegisterRequest(string Email, string Password, string Name, string? PhoneNumber = null, string? CaptchaToken = null);
public record LoginRequest(string Email, string Password);
public record RequestPasswordResetRequest(string Email);
public record VerifyPasswordResetRequest(string Token, string NewPassword);
public record UpdateProfileRequest(string? Name, string? Email, string? PhoneNumber);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record AuthResponse
{
    public string Token { get; init; } = string.Empty;
    public UserResponse User { get; init; } = null!;
}

public record UserResponse
{
    public string Id { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
}
