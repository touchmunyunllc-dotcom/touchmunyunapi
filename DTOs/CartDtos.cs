namespace ECommerce.DTOs;

public record AddToCartRequest(Guid ProductId, int Quantity, string? SelectedColor = null, int? SelectedSize = null);
public record UpdateCartItemRequest(int Quantity);
public record ApplyCouponRequest(string CouponCode);
public record CartItemResponse
{
    public Guid Id { get; init; }
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public decimal ProductPrice { get; init; }
    public string ProductImageUrl { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal Subtotal { get; init; }
    public string? SelectedColor { get; init; }
    public int? SelectedSize { get; init; }
}

public record CouponInfo
{
    public string Code { get; init; } = string.Empty;
    public decimal DiscountValue { get; init; }
    public string DiscountType { get; init; } = string.Empty;
}

public record CartSummaryResponse
{
    public List<CartItemResponse> Items { get; init; } = new();
    public decimal Subtotal { get; init; }
    public decimal Tax { get; init; }
    public decimal Discount { get; init; }
    public decimal Total { get; init; }
    public CouponInfo? AppliedCoupon { get; init; }
}
