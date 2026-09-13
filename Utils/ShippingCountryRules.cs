using System.Text.RegularExpressions;

namespace ECommerce.Utils;

/// <summary>US domestic vs international flat-rate shipping.</summary>
public static partial class ShippingCountryRules
{
    public static bool IsUnitedStates(string? country)
    {
        if (string.IsNullOrWhiteSpace(country))
        {
            return false;
        }

        var trimmed = country.Trim();
        if (trimmed.Length == 2
            && trimmed.Equals("US", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var collapsed = CollapseWhitespaceRegex().Replace(trimmed, " ").ToLowerInvariant();
        collapsed = collapsed.Replace(".", "", StringComparison.Ordinal);

        if (collapsed is "usa" or "u s a" or "us")
        {
            return true;
        }

        if (collapsed.Contains("united states", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex CollapseWhitespaceRegex();
}
