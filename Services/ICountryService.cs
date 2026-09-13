using ECommerce.Models;

namespace ECommerce.Services;

public interface ICountryService
{
    Task<IReadOnlyList<Country>> GetAllAsync();

    Task<bool> ExistsAsync(string countryCode);

    Task SeedCountriesIfEmptyAsync();
}
