namespace ECommerce.Models;

public class Product
{
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
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
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
