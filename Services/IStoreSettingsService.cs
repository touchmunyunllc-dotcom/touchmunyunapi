namespace ECommerce.Services;

public interface IStoreSettingsService
{
    /// <summary>Sales tax rate as a decimal (e.g. 0.10 = 10%).</summary>
    Task<decimal> GetSalesTaxRateAsync();

    Task SetSalesTaxRateAsync(decimal rate);
}
