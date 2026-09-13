using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ECommerce.Configuration;

namespace ECommerce.Services;

public class GoogleGeocodingService : IGeocodingService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GeocodingOptions _options;
    private readonly ILogger<GoogleGeocodingService> _logger;

    public GoogleGeocodingService(
        IHttpClientFactory httpClientFactory,
        IOptions<GeocodingOptions> options,
        ILogger<GoogleGeocodingService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.GoogleApiKey);

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
            var client = _httpClientFactory.CreateClient("GoogleGeocoding");
            var url =
                $"maps/api/geocode/json?address={Uri.EscapeDataString(query)}&key={Uri.EscapeDataString(_options.GoogleApiKey!)}";

            using var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google geocode failed with status {Status}", response.StatusCode);
                return (null, null);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (doc.RootElement.GetProperty("status").GetString() != "OK")
            {
                return (null, null);
            }

            var results = doc.RootElement.GetProperty("results");
            if (results.GetArrayLength() == 0)
            {
                return (null, null);
            }

            var location = results[0].GetProperty("geometry").GetProperty("location");
            var lat = location.GetProperty("lat").GetDouble();
            var lng = location.GetProperty("lng").GetDouble();
            return (
                decimal.Parse(lat.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
                decimal.Parse(lng.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Google geocoding error");
            return (null, null);
        }
    }
}
