namespace ECommerce.Configuration;

public class GeocodingOptions
{
    /// <summary>Mapbox | Google | Nominatim</summary>
    public string Primary { get; set; } = "Nominatim";

    public string? MapboxAccessToken { get; set; }

    public string? GoogleApiKey { get; set; }

    public bool FallbackToNominatim { get; set; } = true;

    /// <summary>Flag orders when checkout device and ship-to geocode differ by more than this (km).</summary>
    public double MismatchDistanceKm { get; set; } = 500;
}
