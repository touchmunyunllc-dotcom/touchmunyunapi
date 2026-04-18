using System.Data;
using Dapper;

namespace ECommerce.Services;

public class OrderCodeService : IOrderCodeService
{
    private readonly IDbConnection _connection;
    private readonly ILogger<OrderCodeService> _logger;

    public OrderCodeService(IDbConnection connection, ILogger<OrderCodeService> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    public async Task<string> GenerateOrderCodeAsync()
    {
        string orderCode = string.Empty;
        bool isUnique = false;
        int attempts = 0;
        const int maxAttempts = 10;

        while (!isUnique && attempts < maxAttempts)
        {
            // Generate order code: ORD-YYYYMMDD-XXXX (e.g., ORD-20241201-A1B2)
            var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
            var randomPart = GenerateRandomString(4);
            orderCode = $"ORD-{datePart}-{randomPart}";

            var existing = await _connection.QueryFirstOrDefaultAsync<string>(
                "SELECT order_code FROM orders WHERE order_code = @OrderCode",
                new { OrderCode = orderCode });

            if (existing == null)
            {
                isUnique = true;
            }
            else
            {
                attempts++;
                await Task.Delay(10); // Small delay to avoid collisions
            }
        }

        if (!isUnique || string.IsNullOrEmpty(orderCode))
        {
            // Fallback: use GUID-based code
            orderCode = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()}";
            _logger.LogWarning("Generated fallback order code after {Attempts} attempts", attempts);
        }

        return orderCode;
    }

    public async Task<string> GenerateTrackingNumberAsync()
    {
        // Generate tracking number: TRK-XXXXXXXX (e.g., TRK-1A2B3C4D)
        string trackingNumber;
        bool isUnique = false;
        int attempts = 0;
        const int maxAttempts = 10;

        while (!isUnique && attempts < maxAttempts)
        {
            trackingNumber = $"TRK-{GenerateRandomString(8).ToUpper()}";

            var existing = await _connection.QueryFirstOrDefaultAsync<string>(
                "SELECT tracking_number FROM orders WHERE tracking_number = @TrackingNumber",
                new { TrackingNumber = trackingNumber });

            if (existing == null)
            {
                isUnique = true;
                return trackingNumber;
            }
            else
            {
                attempts++;
                await Task.Delay(10);
            }
        }

        // Fallback
        trackingNumber = $"TRK-{Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()}";
        _logger.LogWarning("Generated fallback tracking number after {Attempts} attempts", attempts);
        return trackingNumber;
    }

    private string GenerateRandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}

