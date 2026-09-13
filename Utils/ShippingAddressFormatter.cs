namespace ECommerce.Utils;

public static class ShippingAddressFormatter
{
    public static string? Format(
        string? addressLine1,
        string? addressLine2,
        string? city,
        string? state,
        string? postalCode,
        string? country)
    {
        if (string.IsNullOrWhiteSpace(addressLine1))
        {
            return null;
        }

        var line2 = string.IsNullOrWhiteSpace(addressLine2) ? "" : $", {addressLine2.Trim()}";
        var cityPart = string.IsNullOrWhiteSpace(city) ? "" : city.Trim();
        var statePart = string.IsNullOrWhiteSpace(state) ? "" : state.Trim();
        var postal = string.IsNullOrWhiteSpace(postalCode) ? "" : postalCode.Trim();
        var countryPart = string.IsNullOrWhiteSpace(country) ? "" : country.Trim();

        var cityStateZip = string.Join(
            " ",
            new[] { cityPart, statePart, postal }.Where(s => !string.IsNullOrEmpty(s)));

        var tail = string.Join(
            ", ",
            new[] { cityStateZip, countryPart }.Where(s => !string.IsNullOrEmpty(s)));

        return $"{addressLine1.Trim()}{line2}{(string.IsNullOrEmpty(tail) ? "" : $", {tail}")}";
    }
}
