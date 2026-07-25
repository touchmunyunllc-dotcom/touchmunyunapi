using Stripe;
using Stripe.Checkout;

namespace ECommerce.Services;

public class StripeService : IStripeService
{
    private readonly IConfiguration _configuration;

    public StripeService(IConfiguration configuration)
    {
        _configuration = configuration;
        StripeConfiguration.ApiKey = NormalizeStripeSecretKey(_configuration["Stripe:SecretKey"]);
    }

    /// <summary>
    /// Render/env pastes often include wrapping quotes or whitespace, which Stripe rejects as Invalid API Key.
    /// </summary>
    internal static string NormalizeStripeSecretKey(string? raw)
    {
        var key = (raw ?? string.Empty).Trim().Trim('"').Trim('\'');
        if (string.IsNullOrWhiteSpace(key) || !key.StartsWith("sk_", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Stripe secret key is missing or invalid. Set Stripe__SecretKey on the API host to a valid sk_test_... or sk_live_... key from the Stripe Dashboard (not the pk_ publishable key).");
        }

        return key;
    }

    public async Task<CheckoutSessionResult> CreateCheckoutSessionForTotalAsync(
        decimal totalAmount,
        string currency,
        string userId)
    {
        var currencyNorm = string.IsNullOrEmpty(currency) ? "usd" : currency.ToLowerInvariant();
        var options = new SessionCreateOptions
        {
            PaymentMethodTypes = new List<string> { "card" },
            LineItems = new List<SessionLineItemOptions>
            {
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = currencyNorm,
                        UnitAmount = (long)(totalAmount * 100),
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = "Order total"
                        }
                    }
                }
            },
            Mode = "payment",
            SuccessUrl = $"{_configuration["FrontendUrl"]}/order-success?session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = $"{_configuration["FrontendUrl"]}/cart",
            ClientReferenceId = userId
        };

        var service = new SessionService();
        var session = await service.CreateAsync(options);

        return new CheckoutSessionResult(session.Id, session.Url);
    }

    public async Task<PaymentIntentResult> CreatePaymentIntentAsync(
        decimal amount,
        string currency = "usd")
    {
        var options = new PaymentIntentCreateOptions
        {
            Amount = (long)(amount * 100), // Convert to cents
            Currency = currency,
            PaymentMethodTypes = new List<string> { "card" }
        };

        var service = new PaymentIntentService();
        var paymentIntent = await service.CreateAsync(options);

        return new PaymentIntentResult(paymentIntent.ClientSecret, paymentIntent.Id);
    }

    public async Task<PaymentIntent> GetPaymentIntentAsync(
        string paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        var service = new PaymentIntentService();
        return await service.GetAsync(paymentIntentId, cancellationToken: cancellationToken);
    }

    public async Task<Session> GetCheckoutSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var service = new SessionService();
        return await service.GetAsync(sessionId, cancellationToken: cancellationToken);
    }

    public async Task<Session?> FindCheckoutSessionByPaymentIntentAsync(
        string paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        var service = new SessionService();
        var list = await service.ListAsync(
            new SessionListOptions { PaymentIntent = paymentIntentId, Limit = 1 },
            cancellationToken: cancellationToken);
        return list.Data.Count > 0 ? list.Data[0] : null;
    }
}
