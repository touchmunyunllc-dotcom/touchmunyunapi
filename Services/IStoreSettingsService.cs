namespace ECommerce.Services;

public interface IStoreSettingsService
{
    /// <summary>Sales tax rate as a decimal (e.g. 0.10 = 10%).</summary>
    Task<decimal> GetSalesTaxRateAsync();

    Task SetSalesTaxRateAsync(decimal rate);

    Task<decimal> GetShippingStandardUsdAsync();

    Task<decimal> GetShippingInternationalUsdAsync();

    Task SetShippingRatesAsync(decimal standardUsd, decimal internationalUsd);

    /// <summary>Flat shipping from country; requires non-empty country.</summary>
    Task<decimal> GetShippingAmountForCountryAsync(string country);
}
