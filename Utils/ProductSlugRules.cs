using System.Text.RegularExpressions;

namespace ECommerce.Utils;

public static partial class ProductSlugRules
{
    public const int MaxLength = 120;

    /// <summary>URL segment: lowercase letters, digits, and hyphens only.</summary>
    public static string Slugify(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "product";
        }

        var slug = name.Trim().ToLowerInvariant();
        slug = NonAlphanumeric().Replace(slug, "-");
        slug = MultiHyphen().Replace(slug, "-").Trim('-');
        if (slug.Length == 0)
        {
            return "product";
        }

        if (slug.Length > MaxLength)
        {
            slug = slug[..MaxLength].Trim('-');
        }

        return slug.Length > 0 ? slug : "product";
    }

    public static bool LooksLikeGuid(string value)
    {
        return Guid.TryParse(value, out _);
    }

    [GeneratedRegex(@"[^a-z0-9]+", RegexOptions.Compiled)]
    private static partial Regex NonAlphanumeric();

    [GeneratedRegex(@"-+", RegexOptions.Compiled)]
    private static partial Regex MultiHyphen();
}
