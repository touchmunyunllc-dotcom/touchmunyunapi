using System.Text.RegularExpressions;
using ECommerce.Models;

namespace ECommerce.Utils;

public static class ProductCustomizationRules
{
    public const int WristbandNumberMaxLength = 2;
    private static readonly Regex DigitsOnly = new("^[0-9]+$", RegexOptions.Compiled);

    public static void ValidateForCart(Product product, string? selectedColor, string? customNumber, string? writingColor)
    {
        var isWristband = ProductPricingRules.IsWristband(product);

        if (!isWristband)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedColor))
        {
            throw new CartValidationException("Please select a band color for this wristband.");
        }

        if (product.Colors.Count > 0
            && !product.Colors.Any(c => c.Equals(selectedColor.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw new CartValidationException($"Band color '{selectedColor}' is not available for this wristband.");
        }

        if (string.IsNullOrWhiteSpace(customNumber))
        {
            throw new CartValidationException("Please enter a number for this wristband.");
        }

        var number = customNumber.Trim();
        if (number.Length > WristbandNumberMaxLength || !DigitsOnly.IsMatch(number))
        {
            throw new CartValidationException(
                $"Wristband number must be digits only (max {WristbandNumberMaxLength}).");
        }

        var normalizedWriting = NormalizeWritingColor(product, writingColor);
        if (string.IsNullOrWhiteSpace(normalizedWriting))
        {
            throw new CartValidationException("Please select a writing color for this wristband.");
        }
    }

    public static string? NormalizeNumber(string? customNumber)
    {
        if (string.IsNullOrWhiteSpace(customNumber))
        {
            return null;
        }

        return customNumber.Trim();
    }

    public static string? NormalizeWritingColor(Product product, string? writingColor)
    {
        if (string.IsNullOrWhiteSpace(writingColor))
        {
            return null;
        }

        var allowed = product.Colors;

        if (allowed.Count == 0)
        {
            return writingColor.Trim();
        }

        return allowed.FirstOrDefault(c =>
            c.Equals(writingColor.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
