namespace ECommerce.DTOs;

public record CreatePaymentIntentRequest(decimal Amount, string Currency, string? CouponCode, Guid? ShippingAddressId);
public record PaymentIntentResponse(string ClientSecret, string PaymentIntentId, Guid? OrderId);
public record ConfirmStripeCheckoutRequest(string PaymentIntentId);
public record ConfirmStripeCheckoutResponse(Guid OrderId, string OrderCode);
public record CreateCodOrderRequest(decimal Amount, string Currency, string? CouponCode, Guid? ShippingAddressId);
public record CodOrderResponse(string OrderCode, Guid OrderId, string Message);

/// <summary>Hosted Checkout uses server cart totals only (no client line items).</summary>
public record CreateCheckoutSessionRequest(string? CouponCode, Guid? ShippingAddressId, string? Currency);

public record CheckoutSessionResponse(string SessionId, string Url);

public record ResolveCheckoutSessionResponse(
    string? PaymentIntentId,
    Guid? OrderId,
    string? OrderCode,
    bool SessionComplete);
