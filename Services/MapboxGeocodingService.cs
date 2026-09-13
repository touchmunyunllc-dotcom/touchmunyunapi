using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ECommerce.Configuration;

namespace ECommerce.Services;

public class MapboxGeocodingService : IGeocodingService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GeocodingOptions _options;
    private readonly ILogger<MapboxGeocodingService> _logger;

    public MapboxGeocodingService(
        IHttpClientFactory httpClientFactory,
        IOptions<GeocodingOptions> options,
        ILogger<MapboxGeocodingService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.MapboxAccessToken);

    public async Task<(decimal? Latitude, decimal? Longitude)> GeocodeAddressAsync(
        string addressLine1,
        string? addressLine2,
        string city,
        string state,
        string postalCode,
        string countryCode,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return (null, null);
        }

        var parts = new[] { addressLine1, addressLine2, city, state, postalCode, countryCode }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        var query = string.Join(", ", parts);
        if (string.IsNullOrWhiteSpace(query))
        {
            return (null, null);
        }

        try
        {
            var client = _httpClientFactory.CreateClient("MapboxGeocoding");
            var encodedQuery = Uri.EscapeDataString(query);
            var country = countryCode.Trim().ToLowerInvariant();
            var token = Uri.EscapeDataString(_options.MapboxAccessToken!);
            var url =
                $"geocoding/v5/mapbox.places/{encodedQuery}.json?access_token={token}&country={country}&limit=1";

            using var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Mapbox geocode failed with status {Status}", response.StatusCode);
                return (null, null);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var features = doc.RootElement.GetProperty("features");
            if (features.GetArrayLength() == 0)
            {
                return (null, null);
            }

            var center = features[0].GetProperty("center");
            var lon = center[0].GetDouble();
            var lat = center[1].GetDouble();
            return (
                decimal.Parse(lat.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
                decimal.Parse(lon.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Mapbox geocoding error");
            return (null, null);
        }
    }
}
