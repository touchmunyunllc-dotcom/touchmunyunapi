using System.Data;
using System.Globalization;
using Dapper;
using ECommerce.Utils;

namespace ECommerce.Services;

public class StoreSettingsService : IStoreSettingsService
{
    public const string SalesTaxRateKey = "sales_tax_rate";
    public const string ShippingStandardKey = "shipping_standard_usd";
    public const string ShippingInternationalKey = "shipping_international_usd";
    public const decimal DefaultSalesTaxRate = 0.10m;
    public const decimal DefaultShippingStandardUsd = 7m;
    public const decimal DefaultShippingInternationalUsd = 10m;

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

    public async Task<decimal> GetShippingStandardUsdAsync() =>
        await GetDecimalSettingAsync(ShippingStandardKey, DefaultShippingStandardUsd);

    public async Task<decimal> GetShippingInternationalUsdAsync() =>
        await GetDecimalSettingAsync(ShippingInternationalKey, DefaultShippingInternationalUsd);

    public async Task SetShippingRatesAsync(decimal standardUsd, decimal internationalUsd)
    {
        if (standardUsd < 0m || internationalUsd < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(standardUsd), "Shipping amounts must be zero or greater.");
        }

        await UpsertSettingAsync(ShippingStandardKey, standardUsd.ToString("0.##", CultureInfo.InvariantCulture));
        await UpsertSettingAsync(
            ShippingInternationalKey,
            internationalUsd.ToString("0.##", CultureInfo.InvariantCulture));
    }

    public async Task<decimal> GetShippingAmountForCountryAsync(string country)
    {
        if (string.IsNullOrWhiteSpace(country))
        {
            throw new ArgumentException("Country is required to calculate shipping.", nameof(country));
        }

        var amount = ShippingCountryRules.IsUnitedStates(country)
            ? await GetShippingStandardUsdAsync()
            : await GetShippingInternationalUsdAsync();
        return Math.Round(amount, 2, MidpointRounding.AwayFromZero);
    }

    private async Task<decimal> GetDecimalSettingAsync(string key, decimal defaultValue)
    {
        var value = await _connection.QueryFirstOrDefaultAsync<string>(
            "SELECT value FROM store_settings WHERE key = @Key",
            new { Key = key });

        if (decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            && parsed >= 0m)
        {
            return parsed;
        }

        return defaultValue;
    }

    private async Task UpsertSettingAsync(string key, string value)
    {
        await _connection.ExecuteAsync(
            """
            INSERT INTO store_settings (key, value, updated_at)
            VALUES (@Key, @Value, NOW())
            ON CONFLICT (key) DO UPDATE SET value = EXCLUDED.value, updated_at = NOW()
            """,
            new { Key = key, Value = value });
    }
}
