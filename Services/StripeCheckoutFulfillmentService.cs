using System.Data;
using System.Text.Json;
using Dapper;
using ECommerce.Models;
using ECommerce.Utils;
using Microsoft.Extensions.Logging;
using Npgsql;
using Stripe.Checkout;

namespace ECommerce.Services;

public class StripeCheckoutFulfillmentService : IStripeCheckoutFulfillmentService
{
    private readonly IDbConnection _connection;
    private readonly ICartService _cartService;
    private readonly IOrderCodeService _orderCodeService;
    private readonly IAdminNotificationService _adminNotificationService;
    private readonly IStripeService _stripeService;
    private readonly ILogger<StripeCheckoutFulfillmentService> _logger;
    private const int MaxQuantityPerProduct = 10;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public StripeCheckoutFulfillmentService(
        IDbConnection connection,
        ICartService cartService,
        IOrderCodeService orderCodeService,
        IAdminNotificationService adminNotificationService,
        IStripeService stripeService,
        ILogger<StripeCheckoutFulfillmentService> logger)
    {
        _connection = connection;
        _cartService = cartService;
        _orderCodeService = orderCodeService;
        _adminNotificationService = adminNotificationService;
        _stripeService = stripeService;
        _logger = logger;
    }

    public async Task SavePendingCheckoutAsync(string paymentIntentId, StripeCheckoutPendingPayload payload)
    {
        if (_connection.State != ConnectionState.Open)
        {
            _connection.Open();
        }

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        await _connection.ExecuteAsync(
            @"INSERT INTO stripe_checkout_pending (payment_intent_id, payload_json)
              VALUES (@Pi, @Json)",
            new { Pi = paymentIntentId, Json = json });
    }

    public async Task SaveHostedCheckoutPendingAsync(string checkoutSessionId, StripeCheckoutPendingPayload payload)
    {
        if (_connection.State != ConnectionState.Open)
        {
            _connection.Open();
        }

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        await _connection.ExecuteAsync(
            @"INSERT INTO stripe_hosted_checkout_pending (session_id, payload_json)
              VALUES (@Sid, @Json)",
            new { Sid = checkoutSessionId, Json = json });
    }

    public async Task<bool> TryMigrateHostedStripeCheckoutPendingAsync(Session session)
    {
        if (string.IsNullOrEmpty(session.PaymentIntentId))
        {
            return false;
        }

        if (_connection.State != ConnectionState.Open)
        {
            _connection.Open();
        }

        var hostedJson = await _connection.QueryFirstOrDefaultAsync<string>(
            "SELECT payload_json FROM stripe_hosted_checkout_pending WHERE session_id = @Sid",
            new { Sid = session.Id });
        if (string.IsNullOrEmpty(hostedJson))
        {
            return false;
        }

        await _connection.ExecuteAsync(
            @"INSERT INTO stripe_checkout_pending (payment_intent_id, payload_json) VALUES (@Pi, @Json)
              ON CONFLICT (payment_intent_id) DO UPDATE SET payload_json = EXCLUDED.payload_json",
            new { Pi = session.PaymentIntentId, Json = hostedJson });

        await _connection.ExecuteAsync(
            "DELETE FROM stripe_hosted_checkout_pending WHERE session_id = @Sid",
            new { Sid = session.Id });
        return true;
    }

    public async Task<bool> TryMigrateHostedStripeCheckoutPendingByPaymentIntentAsync(string paymentIntentId)
    {
        var session = await _stripeService.FindCheckoutSessionByPaymentIntentAsync(paymentIntentId);
        if (session == null)
        {
            return false;
        }

        return await TryMigrateHostedStripeCheckoutPendingAsync(session);
    }

    public async Task<FulfillmentResult> TryFulfillPaymentIntentAsync(
        string paymentIntentId,
        long amountCents,
        string currency)
    {
        if (_connection.State != ConnectionState.Open)
        {
            _connection.Open();
        }

        var existingOrderId = await _connection.QueryFirstOrDefaultAsync<Guid?>(
            "SELECT id FROM orders WHERE stripe_payment_intent_id = @Pi",
            new { Pi = paymentIntentId });
        if (existingOrderId.HasValue)
        {
            return new FulfillmentResult(true, false, existingOrderId, FulfillmentFailureKind.None);
        }

        var pendingJson = await _connection.QueryFirstOrDefaultAsync<string>(
            "SELECT payload_json FROM stripe_checkout_pending WHERE payment_intent_id = @Pi",
            new { Pi = paymentIntentId });

        if (string.IsNullOrEmpty(pendingJson))
        {
            _logger.LogWarning("Stripe PI {Pi} succeeded but no pending checkout row found", paymentIntentId);
            return new FulfillmentResult(false, false, null, FulfillmentFailureKind.NoPendingCheckout);
        }

        var payload = JsonSerializer.Deserialize<StripeCheckoutPendingPayload>(pendingJson, JsonOptions);
        if (payload == null || payload.Items.Count == 0 || string.IsNullOrEmpty(payload.Kind))
        {
            return new FulfillmentResult(false, false, null, FulfillmentFailureKind.InvalidPayload);
        }

        var paidAmount = amountCents / 100m;
        if (Math.Abs(payload.TotalAmount - paidAmount) > 0.02m)
        {
            _logger.LogError(
                "Stripe PI {Pi} amount mismatch: pending total {Expected} vs paid {Paid}",
                paymentIntentId, payload.TotalAmount, paidAmount);
            return new FulfillmentResult(false, false, null, FulfillmentFailureKind.AmountMismatch);
        }

        try
        {
            return payload.Kind switch
            {
                StripeCheckoutPendingPayload.KindRegistered when payload.UserId.HasValue =>
                    await FulfillRegisteredAsync(paymentIntentId, payload, currency),
                StripeCheckoutPendingPayload.KindGuest =>
                    await FulfillGuestAsync(paymentIntentId, payload, currency),
                _ => new FulfillmentResult(false, false, null, FulfillmentFailureKind.InvalidPayload)
            };
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            var id = await _connection.QueryFirstOrDefaultAsync<Guid?>(
                "SELECT id FROM orders WHERE stripe_payment_intent_id = @Pi",
                new { Pi = paymentIntentId });
            return new FulfillmentResult(true, false, id, FulfillmentFailureKind.None);
        }
    }

    private async Task<FulfillmentResult> FulfillRegisteredAsync(
        string paymentIntentId,
        StripeCheckoutPendingPayload payload,
        string currency)
    {
        var userId = payload.UserId!.Value;
        var orderCode = await _orderCodeService.GenerateOrderCodeAsync();
        var orderId = Guid.NewGuid();

        using var transaction = _connection.BeginTransaction();
        try
        {
            await _connection.ExecuteAsync(@"
                INSERT INTO orders (id, order_code, user_id, total_amount, status, coupon_id, stripe_payment_intent_id, shipping_address_id, created_at)
                VALUES (@Id, @OrderCode, @UserId, @TotalAmount, @Status, @CouponId, @StripePaymentIntentId, @ShippingAddressId, @CreatedAt)",
                new
                {
                    Id = orderId,
                    OrderCode = orderCode,
                    UserId = userId,
                    TotalAmount = payload.TotalAmount,
                    Status = OrderStatus.Paid.ToString(),
                    CouponId = payload.CouponId,
                    StripePaymentIntentId = paymentIntentId,
                    ShippingAddressId = payload.ShippingAddressId,
                    CreatedAt = DateTime.UtcNow
                }, transaction);

            if (payload.TotalAmount >= 1000m || payload.Items.Count >= 20)
            {
                await _adminNotificationService.NotifyHighVolumeOrderAsync(
                    orderCode,
                    payload.TotalAmount,
                    payload.Items.Count);
            }

            foreach (var line in payload.Items)
            {
                if (line.Quantity > MaxQuantityPerProduct)
                {
                    throw new CartValidationException(
                        $"Maximum quantity allowed per product is {MaxQuantityPerProduct}.");
                }

                var rows = await _connection.ExecuteAsync(
                    @"UPDATE products SET available_quantity = available_quantity - @Quantity, updated_at = CURRENT_TIMESTAMP
                      WHERE id = @ProductId AND is_active = TRUE AND available_quantity >= @Quantity",
                    new { line.Quantity, line.ProductId },
                    transaction);

                if (rows != 1)
                {
                    throw new InsufficientStockException($"product {line.ProductId}", 0);
                }

                await _connection.ExecuteAsync(@"
                    INSERT INTO order_items (id, order_id, product_id, quantity, price, created_at)
                    VALUES (@Id, @OrderId, @ProductId, @Quantity, @Price, @CreatedAt)",
                    new
                    {
                        Id = Guid.NewGuid(),
                        OrderId = orderId,
                        line.ProductId,
                        line.Quantity,
                        Price = line.UnitPrice,
                        CreatedAt = DateTime.UtcNow
                    }, transaction);
            }

            await _connection.ExecuteAsync(@"
                INSERT INTO payments (id, order_id, stripe_payment_id, amount, status, currency, created_at)
                VALUES (@Id, @OrderId, @StripePaymentId, @Amount, @Status, @Currency, @CreatedAt)",
                new
                {
                    Id = Guid.NewGuid(),
                    OrderId = orderId,
                    StripePaymentId = paymentIntentId,
                    Amount = payload.TotalAmount,
                    Status = PaymentStatus.Completed.ToString(),
                    Currency = currency,
                    CreatedAt = DateTime.UtcNow
                }, transaction);

            await _connection.ExecuteAsync(
                "DELETE FROM stripe_checkout_pending WHERE payment_intent_id = @Pi",
                new { Pi = paymentIntentId },
                transaction);

            transaction.Commit();
        }
        catch (InsufficientStockException ex)
        {
            transaction.Rollback();
            _logger.LogError(ex,
                "Stock/catalog failure fulfilling Stripe PI {Pi} for user {UserId} — payment succeeded; manual reconciliation may be required",
                paymentIntentId, userId);
            await _adminNotificationService.NotifyFailedPaymentAsync(
                orderCode,
                paymentIntentId,
                payload.TotalAmount,
                ex.Message);
            return new FulfillmentResult(false, false, null, FulfillmentFailureKind.StockOrCatalogError);
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "Failed to fulfill registered Stripe checkout for PI {Pi}", paymentIntentId);
            throw;
        }

        await _cartService.ClearCartAsync(userId);
        return new FulfillmentResult(true, true, orderId, FulfillmentFailureKind.None);
    }

    private async Task<FulfillmentResult> FulfillGuestAsync(
        string paymentIntentId,
        StripeCheckoutPendingPayload payload,
        string currency)
    {
        if (string.IsNullOrWhiteSpace(payload.GuestEmail))
        {
            return new FulfillmentResult(false, false, null, FulfillmentFailureKind.InvalidPayload);
        }

        var orderCode = await _orderCodeService.GenerateOrderCodeAsync();
        var orderId = Guid.NewGuid();

        using var transaction = _connection.BeginTransaction();
        try
        {
            await _connection.ExecuteAsync(@"
                INSERT INTO orders (id, order_code, user_id, guest_email, total_amount, status, coupon_id, stripe_payment_intent_id, created_at)
                VALUES (@Id, @OrderCode, NULL, @GuestEmail, @TotalAmount, @Status, @CouponId, @StripePaymentIntentId, @CreatedAt)",
                new
                {
                    Id = orderId,
                    OrderCode = orderCode,
                    GuestEmail = payload.GuestEmail.Trim(),
                    TotalAmount = payload.TotalAmount,
                    Status = OrderStatus.Paid.ToString(),
                    CouponId = payload.CouponId,
                    StripePaymentIntentId = paymentIntentId,
                    CreatedAt = DateTime.UtcNow
                }, transaction);

            foreach (var line in payload.Items)
            {
                if (line.Quantity > MaxQuantityPerProduct)
                {
                    throw new CartValidationException(
                        $"Maximum quantity allowed per product is {MaxQuantityPerProduct}.");
                }

                var rows = await _connection.ExecuteAsync(
                    @"UPDATE products SET available_quantity = available_quantity - @Quantity, updated_at = CURRENT_TIMESTAMP
                      WHERE id = @ProductId AND is_active = TRUE AND available_quantity >= @Quantity",
                    new { line.Quantity, line.ProductId },
                    transaction);

                if (rows != 1)
                {
                    throw new InsufficientStockException($"product {line.ProductId}", 0);
                }

                await _connection.ExecuteAsync(@"
                    INSERT INTO order_items (id, order_id, product_id, quantity, price, created_at)
                    VALUES (@Id, @OrderId, @ProductId, @Quantity, @Price, @CreatedAt)",
                    new
                    {
                        Id = Guid.NewGuid(),
                        OrderId = orderId,
                        line.ProductId,
                        line.Quantity,
                        Price = line.UnitPrice,
                        CreatedAt = DateTime.UtcNow
                    }, transaction);
            }

            await _connection.ExecuteAsync(@"
                INSERT INTO payments (id, order_id, stripe_payment_id, amount, status, currency, created_at)
                VALUES (@Id, @OrderId, @StripePaymentId, @Amount, @Status, @Currency, @CreatedAt)",
                new
                {
                    Id = Guid.NewGuid(),
                    OrderId = orderId,
                    StripePaymentId = paymentIntentId,
                    Amount = payload.TotalAmount,
                    Status = PaymentStatus.Completed.ToString(),
                    Currency = currency,
                    CreatedAt = DateTime.UtcNow
                }, transaction);

            await _connection.ExecuteAsync(
                "DELETE FROM stripe_checkout_pending WHERE payment_intent_id = @Pi",
                new { Pi = paymentIntentId },
                transaction);

            transaction.Commit();
        }
        catch (InsufficientStockException ex)
        {
            transaction.Rollback();
            _logger.LogError(ex,
                "Stock failure fulfilling guest Stripe PI {Pi} — payment succeeded; manual reconciliation may be required",
                paymentIntentId);
            await _adminNotificationService.NotifyFailedPaymentAsync(
                orderCode,
                paymentIntentId,
                payload.TotalAmount,
                ex.Message);
            return new FulfillmentResult(false, false, null, FulfillmentFailureKind.StockOrCatalogError);
        }
        catch (Exception)
        {
            transaction.Rollback();
            throw;
        }

        return new FulfillmentResult(true, true, orderId, FulfillmentFailureKind.None);
    }
}
