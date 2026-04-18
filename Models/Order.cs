namespace ECommerce.Models;

public enum OrderStatus
{
    Pending,
    Paid,
    Packed,
    Processing,
    Shipped,
    Delivered,
    Cancelled
}

public class Order
{
    public Guid Id { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public User? User { get; set; }
    public string? GuestEmail { get; set; }
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public Guid? CouponId { get; set; }
    public Coupon? Coupon { get; set; }
    public Guid? ShippingAddressId { get; set; }
    public Address? ShippingAddress { get; set; }
    public string? StripePaymentIntentId { get; set; }
    public string? TrackingNumber { get; set; }
    public string? TrackingUrl { get; set; }
    public string? Notes { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<OrderItem> OrderItems { get; set; } = new();

    // Legacy property for backward compatibility
    public decimal Total
    {
        get => TotalAmount;
        set => TotalAmount = value;
    }

    // Legacy property for backward compatibility (renamed to avoid conflict with navigation property)
    public string? ShippingAddressString
    {
        get => ShippingAddressId?.ToString();
        set { /* Legacy - use ShippingAddressId instead */ }
    }
}
