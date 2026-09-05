using ECommerce.Models;

namespace ECommerce.Utils;

/// <summary>Resolves line-item unit prices from admin-configured product fields.</summary>
public static class ProductPricingRules
{
    public static bool IsWristband(Product product) =>
        string.Equals(product.CustomizationType, Product.CustomizationWristband, StringComparison.OrdinalIgnoreCase);

    public static bool IsNoSurchargeColor(Product product, string? color)
    {
        if (string.IsNullOrWhiteSpace(color) || product.NoSurchargeColors.Count == 0)
        {
            return false;
        }

        return product.NoSurchargeColors.Any(c =>
            c.Equals(color.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static decimal GetColorSurcharge(Product product, string? selectedColor)
    {
        if (!IsWristband(product) || product.ColorSurcharge <= 0)
        {
            return 0m;
        }

        return IsNoSurchargeColor(product, selectedColor) ? 0m : product.ColorSurcharge;
    }

    /// <summary>Unit price for cart/checkout. Uses product price/sale price; optional per-color surcharge.</summary>
    public static decimal ResolveUnitPrice(Product product, string? selectedColor)
    {
        var basePrice = product.DisplayPrice;
        return basePrice + GetColorSurcharge(product, selectedColor);
    }
}
