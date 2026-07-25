using System.Text.RegularExpressions;
using ECommerce.Models;

namespace ECommerce.Utils;

public static class ProductCustomizationRules
{
    public const int WristbandNumberMaxLength = 3;
    public static readonly string[] WritingColors = { "Black", "White", "Red" };
    private static readonly Regex DigitsOnly = new("^[0-9]+$", RegexOptions.Compiled);

    public static void ValidateForCart(Product product, string? selectedColor, string? customNumber, string? writingColor)
    {
        var isWristband = string.Equals(
            product.CustomizationType,
            Product.CustomizationWristband,
            StringComparison.OrdinalIgnoreCase);

        if (!isWristband)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedColor))
        {
            throw new CartValidationException("Please select a band color for this wristband.");
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

        if (string.IsNullOrWhiteSpace(writingColor)
            || !WritingColors.Any(c => c.Equals(writingColor.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw new CartValidationException("Please select a writing color (Black, White, or Red).");
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

    public static string? NormalizeWritingColor(string? writingColor)
    {
        if (string.IsNullOrWhiteSpace(writingColor))
        {
            return null;
        }

        return WritingColors.FirstOrDefault(c =>
            c.Equals(writingColor.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
