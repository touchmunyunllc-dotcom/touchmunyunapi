namespace ECommerce.DTOs;

public record ContactFormRequest
{
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? CaptchaToken { get; init; }
}
