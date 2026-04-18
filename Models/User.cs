namespace ECommerce.Models;

public enum UserRole
{
    Customer,
    Admin
}

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Customer;
    public int CancellationCount { get; set; } = 0;
    public DateTime? OrderBlockedUntil { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Provider { get; set; } // e.g., "Google", "Facebook", "LinkedIn"
    public string? ProviderId { get; set; } // Provider's user ID
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

