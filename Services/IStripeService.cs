using Stripe;
using Stripe.Checkout;

namespace ECommerce.Services;

public interface IStripeService
{
    /// <summary>Single line item for full cart total (tax/coupon already in amount).</summary>
    Task<CheckoutSessionResult> CreateCheckoutSessionForTotalAsync(
        decimal totalAmount,
        string currency,
        string userId);

    Task<PaymentIntentResult> CreatePaymentIntentAsync(
        decimal amount,
        string currency = "usd");

    Task<PaymentIntent> GetPaymentIntentAsync(string paymentIntentId, CancellationToken cancellationToken = default);

    Task<Session> GetCheckoutSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>Finds the Checkout Session created for a PaymentIntent (hosted checkout).</summary>
    Task<Session?> FindCheckoutSessionByPaymentIntentAsync(string paymentIntentId, CancellationToken cancellationToken = default);
}

public record CheckoutSessionResult(string SessionId, string Url);
public record PaymentIntentResult(string ClientSecret, string PaymentIntentId);
public record CheckoutItem(string ProductId, string Name, decimal Price, int Quantity, string? Image);

