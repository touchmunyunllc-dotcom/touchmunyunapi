using System.Data;
using System.Globalization;
using Dapper;

namespace ECommerce.Services;

public class StoreSettingsService : IStoreSettingsService
{
    public const string SalesTaxRateKey = "sales_tax_rate";
    public const decimal DefaultSalesTaxRate = 0.10m;

    private readonly IDbConnection _connection;

    public StoreSettingsService(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<decimal> GetSalesTaxRateAsync()
    {
        var value = await _connection.QueryFirstOrDefaultAsync<string>(
            "SELECT value FROM store_settings WHERE key = @Key",
            new { Key = SalesTaxRateKey });

        if (decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var rate)
            && rate >= 0m
            && rate <= 1m)
        {
            return rate;
        }

        return DefaultSalesTaxRate;
    }

    public async Task SetSalesTaxRateAsync(decimal rate)
    {
        if (rate < 0m || rate > 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(rate), "Tax rate must be between 0 and 1 (e.g. 0.10 for 10%).");
        }

        var formatted = rate.ToString("0.####", CultureInfo.InvariantCulture);
        await _connection.ExecuteAsync(
            """
            INSERT INTO store_settings (key, value, updated_at)
            VALUES (@Key, @Value, NOW())
            ON CONFLICT (key) DO UPDATE SET value = EXCLUDED.value, updated_at = NOW()
            """,
            new { Key = SalesTaxRateKey, Value = formatted });
    }
}
