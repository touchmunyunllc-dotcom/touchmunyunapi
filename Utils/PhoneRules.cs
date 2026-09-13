using PhoneNumbers;

namespace ECommerce.Utils;

public static class PhoneRules
{
    private static readonly PhoneNumberUtil Util = PhoneNumberUtil.GetInstance();

    public static bool TryValidate(
        string? phone,
        string? countryCode,
        out string errorMessage,
        bool required = true)
    {
        errorMessage = string.Empty;
        if (!TryParseMobile(phone, countryCode, out _, out errorMessage, required))
        {
            return false;
        }

        return true;
    }

    public static bool TryNormalizeToE164(
        string? phone,
        string? countryCode,
        out string e164,
        out string errorMessage,
        bool required = true)
    {
        e164 = string.Empty;
        if (!TryParseMobile(phone, countryCode, out var parsed, out errorMessage, required))
        {
            return false;
        }

        e164 = Util.Format(parsed, PhoneNumberFormat.E164);
        return true;
    }

    private static bool TryParseMobile(
        string? phone,
        string? countryCode,
        out PhoneNumber parsed,
        out string errorMessage,
        bool required)
    {
        parsed = new PhoneNumber();
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(phone))
        {
            if (required)
            {
                errorMessage = "Mobile number is required";
                return false;
            }

            return true;
        }

        var trimmed = phone.Trim();
        if (trimmed.Length > 30)
        {
            errorMessage = "Phone number must not exceed 30 characters";
            return false;
        }

        var country = string.IsNullOrWhiteSpace(countryCode)
            ? string.Empty
            : countryCode.Trim().ToUpperInvariant();

        if (!trimmed.StartsWith("+", StringComparison.Ordinal) && string.IsNullOrEmpty(country))
        {
            errorMessage = "Select a country or enter your number starting with + and country code.";
            return false;
        }

        try
        {
            parsed = trimmed.StartsWith("+", StringComparison.Ordinal)
                ? Util.Parse(trimmed, null)
                : Util.Parse(trimmed, country);
        }
        catch (NumberParseException)
        {
            errorMessage = "Enter a valid mobile number for the selected country.";
            return false;
        }

        if (!Util.IsValidNumber(parsed))
        {
            errorMessage = "Enter a valid mobile number for the selected country.";
            return false;
        }

        if (!IsMobileLike(Util.GetNumberType(parsed)))
        {
            errorMessage = "Enter a mobile number (not a landline or special service number).";
            return false;
        }

        if (!string.IsNullOrEmpty(country))
        {
            var numberRegion = Util.GetRegionCodeForNumber(parsed);
            if (!string.IsNullOrEmpty(numberRegion)
                && !numberRegion.Equals(country, StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "This mobile number does not match the selected country.";
                return false;
            }
        }

        return true;
    }

    private static bool IsMobileLike(PhoneNumberType type) =>
        type is PhoneNumberType.MOBILE
            or PhoneNumberType.FIXED_LINE_OR_MOBILE
            or PhoneNumberType.PERSONAL_NUMBER;
}
