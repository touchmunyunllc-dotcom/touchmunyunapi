namespace ECommerce.DTOs;

/// <summary>Server-side totals for guest cart (matches CreateGuestOrderAsync math).</summary>
public record GuestCheckoutPreviewRequest(
    List<GuestOrderItemRequest> Items,
    string? CouponCode,
    string? ShippingCountry = null);

public record GuestCheckoutPreviewResponse(
    decimal Subtotal,
    decimal Tax,
    decimal Shipping,
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
    string? CaptchaToken = null,
    decimal? CheckoutLatitude = null,
    decimal? CheckoutLongitude = null);

public record GuestOrderItemRequest(
    Guid ProductId,
    string Name,
    decimal Price,
    int Quantity,
    string? SelectedColor = null,
    string? SelectedSize = null,
    string? CustomNumber = null,
    string? WritingColor = null);

public record GuestAddressRequest(
    string AddressLine1,
    string? AddressLine2,
    string City,
    string State,
    string PostalCode,
    string Country,
    string Phone);

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
