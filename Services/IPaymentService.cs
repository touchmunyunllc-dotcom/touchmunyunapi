using ECommerce.Models;

namespace ECommerce.Services;

public interface IPaymentService
{
    Task<PaymentIntentWithOrderResult> CreatePaymentIntentWithOrderAsync(
        Guid userId,
        decimal amount,
        string currency,
        string? couponCode = null,
        Guid? shippingAddressId = null);

    Task<CodOrderResult> CreateCodOrderAsync(
        Guid userId,
        decimal amount,
        string currency,
        string? couponCode = null,
        Guid? shippingAddressId = null);

    /// <summary>Cart snapshot for Stripe Hosted Checkout / deferred fulfillment (same rules as PaymentIntent flow).</summary>
    Task<StripeCheckoutPendingPayload> BuildRegisteredCheckoutPayloadAsync(
        Guid userId,
        string? couponCode = null,
        Guid? shippingAddressId = null,
        string currency = "usd");
}

public record PaymentIntentWithOrderResult(
    string ClientSecret,
    string PaymentIntentId,
    Guid? OrderId);

public record CodOrderResult(
    string OrderCode,
    Guid OrderId,
    string Message);

