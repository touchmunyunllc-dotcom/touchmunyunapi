namespace ECommerce.DTOs;

/// <summary>Server-side totals for guest cart (matches CreateGuestOrderAsync math).</summary>
public record GuestCheckoutPreviewRequest(
    List<GuestOrderItemRequest> Items,
    string? CouponCode);

public record GuestCheckoutPreviewResponse(
    decimal Subtotal,
    decimal Tax,
    decimal TotalAmount,
    bool CouponApplied);

public record GuestCheckoutRequest(
    string Email,
    string Name,
    List<GuestOrderItemRequest> Items,
    decimal TotalAmount,
    string Currency,
    string? CouponCode,
    GuestAddressRequest ShippingAddress,
    string? CaptchaToken = null);

public record GuestOrderItemRequest(
    Guid ProductId,
    string Name,
    decimal Price,
    int Quantity);

public record GuestAddressRequest(
    string AddressLine1,
    string? AddressLine2,
    string City,
    string State,
    string PostalCode,
    string Country);

public record GuestOrderResponse(
    string? OrderCode,
    string? ClientSecret,
    string? PaymentIntentId,
    Guid? OrderId,
    string? GuestEmail = null,
    decimal? TotalAmount = null,
    string? Status = null,
    string? TrackingNumber = null,
    string? TrackingUrl = null,
    List<Models.OrderItem>? OrderItems = null);
