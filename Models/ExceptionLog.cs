namespace ECommerce.Models;

public class ExceptionLog
{
    public Guid Id { get; set; }
    public string Severity { get; set; } = "Error";
    public string ExceptionType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? HttpMethod { get; set; }
    public string? RequestPath { get; set; }
    public int? StatusCode { get; set; }
    public string? CorrelationId { get; set; }
    public Guid? UserId { get; set; }
    public string? ClientIp { get; set; }
    public string? AdditionalDataJson { get; set; }
    public DateTime CreatedAt { get; set; }
}
