using ECommerce.Models;
using Stripe.Checkout;

namespace ECommerce.Services;

public interface IStripeCheckoutFulfillmentService
{
    Task SavePendingCheckoutAsync(string paymentIntentId, StripeCheckoutPendingPayload payload);

    Task SaveHostedCheckoutPendingAsync(string checkoutSessionId, StripeCheckoutPendingPayload payload);

    /// <summary>Moves snapshot from stripe_hosted_checkout_pending to stripe_checkout_pending for this session/PI.</summary>
    Task<bool> TryMigrateHostedStripeCheckoutPendingAsync(Session session);

    /// <summary>When payment_intent.succeeded fires before checkout.session.completed, find the Checkout Session and migrate hosted pending.</summary>
    Task<bool> TryMigrateHostedStripeCheckoutPendingByPaymentIntentAsync(string paymentIntentId);

    /// <summary>
    /// Creates order, decrements stock, clears cart (registered), removes pending row. Idempotent if order already exists for PI.
    /// </summary>
    Task<FulfillmentResult> TryFulfillPaymentIntentAsync(string paymentIntentId, long amountCents, string currency);
}

public enum FulfillmentFailureKind
{
    None,
    NoPendingCheckout,
    AmountMismatch,
    StockOrCatalogError,
    InvalidPayload,
    Unknown
}

public sealed record FulfillmentResult(
    bool Ok,
    bool NewlyCreated,
    Guid? OrderId,
    FulfillmentFailureKind Failure);
