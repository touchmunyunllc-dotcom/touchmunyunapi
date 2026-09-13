namespace ECommerce.Services;

public interface IGeocodingService
{
    /// <summary>Forward-geocode a postal address to WGS84 coordinates (best effort).</summary>
    Task<(decimal? Latitude, decimal? Longitude)> GeocodeAddressAsync(
        string addressLine1,
        string? addressLine2,
        string city,
        string state,
        string postalCode,
        string countryCode,
        CancellationToken cancellationToken = default);
}
