using System.Text.RegularExpressions;

namespace ECommerce.Utils;

public static partial class PostalCodeRules
{
    public static bool TryValidate(string? postalCode, string? countryCode, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(postalCode))
        {
            errorMessage = "Postal code is required";
            return false;
        }

        var trimmed = postalCode.Trim();
        if (trimmed.Length > 20)
        {
            errorMessage = "Postal code must not exceed 20 characters";
            return false;
        }

        var country = string.IsNullOrWhiteSpace(countryCode)
            ? string.Empty
            : countryCode.Trim().ToUpperInvariant();

        return country switch
        {
            "US" => ValidateUs(trimmed, out errorMessage),
            "CA" => ValidateCanada(trimmed, out errorMessage),
            "IN" => ValidateIndia(trimmed, out errorMessage),
            "GB" => ValidateUk(trimmed, out errorMessage),
            "AU" => ValidateAustralia(trimmed, out errorMessage),
            _ => ValidateGenericInternational(trimmed, out errorMessage),
        };
    }

    private static bool ValidateUs(string postal, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (UsZipRegex().IsMatch(postal))
        {
            return true;
        }

        errorMessage = "Enter a valid US ZIP code (e.g. 10001 or 10001-1234).";
        return false;
    }

    private static bool ValidateCanada(string postal, out string errorMessage)
    {
        errorMessage = string.Empty;
        var normalized = postal.ToUpperInvariant().Replace(" ", string.Empty);
        if (CanadaPostalRegex().IsMatch(normalized))
        {
            return true;
        }

        errorMessage = "Enter a valid Canadian postal code (e.g. K1A 0B1).";
        return false;
    }

    private static bool ValidateIndia(string postal, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (IndiaPinRegex().IsMatch(postal))
        {
            return true;
        }

        errorMessage = "Enter a valid 6-digit PIN code.";
        return false;
    }

    private static bool ValidateUk(string postal, out string errorMessage)
    {
        errorMessage = string.Empty;
        var normalized = postal.ToUpperInvariant();
        if (UkPostcodeRegex().IsMatch(normalized))
        {
            return true;
        }

        errorMessage = "Enter a valid UK postcode (e.g. SW1A 1AA).";
        return false;
    }

    private static bool ValidateAustralia(string postal, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (AustraliaPostcodeRegex().IsMatch(postal))
        {
            return true;
        }

        errorMessage = "Enter a valid 4-digit Australian postcode.";
        return false;
    }

    private static bool ValidateGenericInternational(string postal, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (postal.Length < 3 || postal.Length > 16)
        {
            errorMessage = "Enter a valid postal or ZIP code (3–16 characters).";
            return false;
        }

        if (!GenericPostalCharsRegex().IsMatch(postal))
        {
            errorMessage = "Postal code may only contain letters, numbers, spaces, and hyphens.";
            return false;
        }

        if (!GenericPostalHasDigitRegex().IsMatch(postal))
        {
            errorMessage = "Enter a valid postal or ZIP code for the selected country.";
            return false;
        }

        return true;
    }

    [GeneratedRegex(@"^\d{5}(-\d{4})?$")]
    private static partial Regex UsZipRegex();

    [GeneratedRegex(@"^[ABCEGHJ-NPRSTVXY]\d[ABCEGHJ-NPRSTV-Z]\d[ABCEGHJ-NPRSTV-Z]\d$", RegexOptions.IgnoreCase)]
    private static partial Regex CanadaPostalRegex();

    [GeneratedRegex(@"^\d{6}$")]
    private static partial Regex IndiaPinRegex();

    [GeneratedRegex(@"^(GIR\s?0AA|[A-Z]{1,2}\d[A-Z\d]?\s?\d[A-Z]{2})$", RegexOptions.IgnoreCase)]
    private static partial Regex UkPostcodeRegex();

    [GeneratedRegex(@"^\d{4}$")]
    private static partial Regex AustraliaPostcodeRegex();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9 \-]*[A-Za-z0-9]$|^[A-Za-z0-9]{2,3}$")]
    private static partial Regex GenericPostalCharsRegex();

    [GeneratedRegex(@"\d")]
    private static partial Regex GenericPostalHasDigitRegex();
}
