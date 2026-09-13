using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ECommerce.Services;

public class NominatimGeocodingService : IGeocodingService
{
    private static readonly SemaphoreSlim RateGate = new(1, 1);
    private static DateTime _lastRequestUtc = DateTime.MinValue;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NominatimGeocodingService> _logger;

    public NominatimGeocodingService(IHttpClientFactory httpClientFactory, ILogger<NominatimGeocodingService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<(decimal? Latitude, decimal? Longitude)> GeocodeAddressAsync(
        string addressLine1,
        string? addressLine2,
        string city,
        string state,
        string postalCode,
        string countryCode,
        CancellationToken cancellationToken = default)
    {
        var parts = new[] { addressLine1, addressLine2, city, state, postalCode, countryCode }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        var query = string.Join(", ", parts);
        if (string.IsNullOrWhiteSpace(query))
        {
            return (null, null);
        }

        try
        {
            await RateGate.WaitAsync(cancellationToken);
            try
            {
                var elapsed = DateTime.UtcNow - _lastRequestUtc;
                if (elapsed < TimeSpan.FromSeconds(1.1))
                {
                    await Task.Delay(TimeSpan.FromSeconds(1.1) - elapsed, cancellationToken);
                }

                var client = _httpClientFactory.CreateClient("Nominatim");
                var country = countryCode.Trim().ToLowerInvariant();
                var url =
                    $"search?q={Uri.EscapeDataString(query)}&countrycodes={Uri.EscapeDataString(country)}&format=json&limit=1";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                using var response = await client.SendAsync(request, cancellationToken);
                _lastRequestUtc = DateTime.UtcNow;

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Nominatim geocode failed with status {Status}", response.StatusCode);
                    return (null, null);
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                {
                    return (null, null);
                }

                var first = doc.RootElement[0];
                if (!first.TryGetProperty("lat", out var latEl) || !first.TryGetProperty("lon", out var lonEl))
                {
                    return (null, null);
                }

                if (!decimal.TryParse(latEl.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
                    || !decimal.TryParse(lonEl.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
                {
                    return (null, null);
                }

                return (lat, lon);
            }
            finally
            {
                RateGate.Release();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Geocoding error for address query");
            return (null, null);
        }
    }
}
