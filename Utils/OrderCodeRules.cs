namespace ECommerce.Utils;

/// <summary>Storefront order reference — always alphanumeric codes (e.g. ORD-20250913-A1B2), never raw GUIDs.</summary>
public static class OrderCodeRules
{
    public const string UnknownPlaceholder = "ORD-UNKNOWN";

    public static string Display(string? orderCode)
    {
        if (string.IsNullOrWhiteSpace(orderCode))
        {
            return UnknownPlaceholder;
        }

        var trimmed = orderCode.Trim();
        if (Guid.TryParse(trimmed, out _))
        {
            return UnknownPlaceholder;
        }

        return trimmed;
    }
}
