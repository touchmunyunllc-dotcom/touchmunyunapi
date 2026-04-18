namespace ECommerce.Services;

public interface IOrderCodeService
{
    Task<string> GenerateOrderCodeAsync();
    Task<string> GenerateTrackingNumberAsync();
}

