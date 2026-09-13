using Microsoft.Extensions.Options;
using ECommerce.Configuration;

namespace ECommerce.Services;

/// <summary>Tries configured production geocoder first, then Nominatim fallback.</summary>
public class ChainedGeocodingService : IGeocodingService
{
    private readonly GeocodingOptions _options;
    private readonly MapboxGeocodingService _mapbox;
    private readonly GoogleGeocodingService _google;
    private readonly NominatimGeocodingService _nominatim;

    public ChainedGeocodingService(
        IOptions<GeocodingOptions> options,
        MapboxGeocodingService mapbox,
        GoogleGeocodingService google,
        NominatimGeocodingService nominatim,
        ILogger<ChainedGeocodingService> logger)
    {
        _options = options.Value;
        _mapbox = mapbox;
        _google = google;
        _nominatim = nominatim;
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
        foreach (var provider in BuildProviders())
        {
            var result = await provider.GeocodeAddressAsync(
                addressLine1, addressLine2, city, state, postalCode, countryCode, cancellationToken);
            if (result.Latitude is not null && result.Longitude is not null)
            {
                return result;
            }
        }

        return (null, null);
    }

    private IEnumerable<IGeocodingService> BuildProviders()
    {
        var primary = (_options.Primary ?? "Nominatim").Trim().ToLowerInvariant();
        IGeocodingService? primaryService = primary switch
        {
            "mapbox" when _mapbox.IsConfigured => _mapbox,
            "google" when _google.IsConfigured => _google,
            "nominatim" => _nominatim,
            _ when _mapbox.IsConfigured => _mapbox,
            _ when _google.IsConfigured => _google,
            _ => null
        };

        if (primaryService != null)
        {
            yield return primaryService;
        }

        if (primaryService == _nominatim)
        {
            yield break;
        }

        if (_options.FallbackToNominatim || primaryService == null)
        {
            yield return _nominatim;
        }
    }
}
