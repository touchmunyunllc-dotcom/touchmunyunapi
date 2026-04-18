namespace ECommerce.Models;

public enum DiscountType
{
    Percentage,
    FixedAmount
}

public class Coupon
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public DiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int? UsageLimit { get; set; }
    public int UsageCount { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public decimal MinPurchaseAmount { get; set; } = 0;
    public decimal? MaxDiscountAmount { get; set; }
    public DateTime CreatedAt { get; set; }

    // Legacy property for backward compatibility
    public decimal DiscountAmount
    {
        get => DiscountValue;
        set => DiscountValue = value;
    }

    // Legacy property for backward compatibility
    public DateTime? ExpiresAt
    {
        get => ExpiryDate;
        set => ExpiryDate = value;
    }
}
