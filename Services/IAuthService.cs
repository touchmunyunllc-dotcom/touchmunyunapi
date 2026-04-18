using ECommerce.Models;

namespace ECommerce.Services;

public interface IAuthService
{
    Task<(bool Success, User? User, string? ErrorMessage)> RegisterAsync(string email, string password, string name, string? phoneNumber = null, string? clientIp = null);
    Task<(bool Success, User? User, string? Token, string? ErrorMessage)> LoginAsync(string email, string password);
    Task<User?> GetUserByIdAsync(Guid userId);
    Task<User?> GetUserByEmailAsync(string email);
    Task<bool> VerifyPasswordAsync(string password, string passwordHash);
    Task<string> GenerateJwtTokenAsync(User user);
    Task<string> GenerateRefreshTokenAsync(User user);
    Task<(bool Success, string? AccessToken, string? RefreshToken, string? ErrorMessage)> RotateRefreshTokenAsync(string refreshToken);
    Task<bool> RequestPasswordResetAsync(string email, string? clientIp = null);
    Task<bool> VerifyPasswordResetAsync(string token, string newPassword);
    Task<bool> LogoutAsync(Guid userId);
    Task<(bool Success, User? User, string? ErrorMessage)> UpdateProfileAsync(Guid userId, string? name, string? email, string? phoneNumber = null);
    Task<(bool Success, string? ErrorMessage)> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);
    Task<(bool Success, User? User, string? Token, string? ErrorMessage)> SocialLoginAsync(
        string provider, string providerId, string email, string name);
}
