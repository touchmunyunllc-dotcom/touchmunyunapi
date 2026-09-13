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
    public string? GuestName { get; set; }
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
    public decimal? ShippingLatitude { get; set; }
    public decimal? ShippingLongitude { get; set; }
    public decimal? CheckoutLatitude { get; set; }
    public decimal? CheckoutLongitude { get; set; }
    public DateTime? CheckoutLocationAt { get; set; }
    public DateTime? ShippingGeocodedAt { get; set; }
    public decimal? LocationDistanceKm { get; set; }
    public bool LocationMismatchFlag { get; set; }
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
