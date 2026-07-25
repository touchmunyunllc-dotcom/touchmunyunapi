using System.Text.Json;
using System.Text.Json.Serialization;

namespace ECommerce.Models;

public class Product
{
    public const string CustomizationWristband = "wristband";

    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? SalePrice { get; set; }
    public List<string> Images { get; set; } = new();
    public int AvailableQuantity { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public List<string> Colors { get; set; } = new();
    public List<int> Sizes { get; set; } = new();
    /// <summary>Map of color name → image URL (e.g. Black → https://...).</summary>
    public Dictionary<string, string> ColorImages { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Dapper maps JSONB text here; ignored in API JSON.</summary>
    [JsonIgnore]
    public string? ColorImagesJson { get; set; }
    /// <summary>null or "wristband".</summary>
    public string? CustomizationType { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public void HydrateColorImages()
    {
        if (string.IsNullOrWhiteSpace(ColorImagesJson))
        {
            ColorImages ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(ColorImagesJson)
                         ?? new Dictionary<string, string>();
            ColorImages = new Dictionary<string, string>(parsed, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            ColorImages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public string ResolveImageForColor(string? color)
    {
        if (!string.IsNullOrWhiteSpace(color)
            && ColorImages.TryGetValue(color.Trim(), out var url)
            && !string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        return ImageUrl;
    }

    public static string? SerializeColorImages(Dictionary<string, string>? colorImages)
    {
        if (colorImages == null || colorImages.Count == 0)
        {
            return "{}";
        }

        return JsonSerializer.Serialize(colorImages);
    }
    
    // Computed property for display price
    public decimal DisplayPrice => SalePrice ?? Price;
    
    // Computed property for discount percentage
    public decimal? DiscountPercentage => SalePrice.HasValue && SalePrice < Price
        ? Math.Round(((Price - SalePrice.Value) / Price) * 100, 0)
        : null;

    // Legacy property for backward compatibility
    public string ImageUrl
    {
        get => Images.FirstOrDefault() ?? string.Empty;
        set
        {
            if (!string.IsNullOrEmpty(value) && !Images.Contains(value))
            {
                Images = new List<string> { value };
            }
        }
    }

    // Legacy property for backward compatibility
    public int Stock
    {
        get => AvailableQuantity;
        set => AvailableQuantity = value;
    }
}
