namespace ECommerce.Utils;

public static class CountryCodeHelper
{
    public static string NormalizeToIsoCode(string? country)
    {
        if (string.IsNullOrWhiteSpace(country))
        {
            throw new ArgumentException("Country is required.", nameof(country));
        }

        var trimmed = country.Trim();
        if (trimmed.Length == 2)
        {
            return trimmed.ToUpperInvariant();
        }

        if (ShippingCountryRules.IsUnitedStates(trimmed))
        {
            return "US";
        }

        throw new ArgumentException("Country must be a 2-letter ISO code (e.g. US, CA).", nameof(country));
    }
}
