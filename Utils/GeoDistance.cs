namespace ECommerce.Utils;

public static class GeoDistance
{
    public static double HaversineKilometers(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371.0;
        static double ToRad(double deg) => deg * Math.PI / 180.0;

        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
            * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusKm * c;
    }

    public static double? HaversineKilometers(decimal? lat1, decimal? lon1, decimal? lat2, decimal? lon2)
    {
        if (lat1 is null || lon1 is null || lat2 is null || lon2 is null)
        {
            return null;
        }

        return HaversineKilometers(
            (double)lat1.Value,
            (double)lon1.Value,
            (double)lat2.Value,
            (double)lon2.Value);
    }
}
