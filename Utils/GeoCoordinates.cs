namespace ECommerce.Utils;

public static class GeoCoordinates
{
    public static bool TryValidate(decimal? latitude, decimal? longitude, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (latitude is null && longitude is null)
        {
            return true;
        }

        if (latitude is null || longitude is null)
        {
            errorMessage = "Latitude and longitude must both be provided.";
            return false;
        }

        if (latitude < -90m || latitude > 90m)
        {
            errorMessage = "Latitude must be between -90 and 90.";
            return false;
        }

        if (longitude < -180m || longitude > 180m)
        {
            errorMessage = "Longitude must be between -180 and 180.";
            return false;
        }

        return true;
    }
}
