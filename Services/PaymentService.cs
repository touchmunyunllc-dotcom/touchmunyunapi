using ECommerce.Data;
using ECommerce.Models;
using ECommerce.Utils;
using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;

namespace ECommerce.Services;

public class PaymentService : IPaymentService
{
    private readonly IDbConnection _connection;
    private readonly IStripeService _stripeService;
    private readonly ICartService _cartService;
    private readonly IOrderCodeService _orderCodeService;
    private readonly IAdminNotificationService _adminNotificationService;
    private readonly IEmailService _emailService;
    private readonly IStripeCheckoutFulfillmentService _stripeCheckoutFulfillment;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IDbConnection connection,
        IStripeService stripeService,
        ICartService cartService,
        IOrderCodeService orderCodeService,
        IAdminNotificationService adminNotificationService,
        IEmailService emailService,
        IStripeCheckoutFulfillmentService stripeCheckoutFulfillment,
        ILogger<PaymentService> logger)
    {
        _connection = connection;
        _stripeService = stripeService;
        _cartService = cartService;
        _orderCodeService = orderCodeService;
        _adminNotificationService = adminNotificationService;
        _emailService = emailService;
        _stripeCheckoutFulfillment = stripeCheckoutFulfillment;
        _logger = logger;
    }

    public async Task<StripeCheckoutPendingPayload> BuildRegisteredCheckoutPayloadAsync(
        Guid userId,
        string? couponCode = null,
        Guid? shippingAddressId = null,
        string currency = "usd")
    {
        if (_connection.State != ConnectionState.Open)
        {
            _connection.Open();
        }

        var cart = await _cartService.GetCartAsync(userId, couponCode);

        if (cart.Items.Count == 0)
        {
            throw new CartValidationException("Cart is empty");
        }

        const int MAX_QUANTITY_PER_PRODUCT = 10;
        foreach (var cartItem in cart.Items)
        {
            if (cartItem.Quantity > MAX_QUANTITY_PER_PRODUCT)
            {
                throw new CartValidationException(
                    $"Maximum quantity allowed per product is {MAX_QUANTITY_PER_PRODUCT}. Product {cartItem.Product?.Name ?? cartItem.ProductId.ToString()} has quantity {cartItem.Quantity}.");
            }
        }

        return new StripeCheckoutPendingPayload
        {
            Kind = StripeCheckoutPendingPayload.KindRegistered,
            UserId = userId,
            TotalAmount = cart.Total,
            Currency = string.IsNullOrWhiteSpace(currency) ? "usd" : currency.ToLowerInvariant(),
            CouponId = cart.AppliedCoupon?.Id,
            ShippingAddressId = shippingAddressId,
            Items = cart.Items.Select(ci => new PendingCheckoutLineItem
            {
                ProductId = ci.ProductId,
                Quantity = ci.Quantity,
                UnitPrice = ci.Product != null ? ci.Product.DisplayPrice : 0
            }).ToList()
        };
    }

    public async Task<PaymentIntentWithOrderResult> CreatePaymentIntentWithOrderAsync(
        Guid userId,
        decimal amount,
        string currency,
        string? couponCode = null,
        Guid? shippingAddressId = null)
    {
        var payload = await BuildRegisteredCheckoutPayloadAsync(userId, couponCode, shippingAddressId, currency);

        if (Math.Abs(amount - payload.TotalAmount) > 0.01m)
        {
            throw new AmountMismatchException(payload.TotalAmount, amount);
        }

        // PaymentIntent only — order, stock, and cart are applied after payment succeeds (webhook / confirm).
        var paymentIntent = await _stripeService.CreatePaymentIntentAsync(amount, currency);

        await _stripeCheckoutFulfillment.SavePendingCheckoutAsync(paymentIntent.PaymentIntentId, payload);

        return new PaymentIntentWithOrderResult(
            paymentIntent.ClientSecret,
            paymentIntent.PaymentIntentId,
            null);
    }

    public async Task<CodOrderResult> CreateCodOrderAsync(
        Guid userId,
        decimal amount,
        string currency,
        string? couponCode = null,
        Guid? shippingAddressId = null)
    {
        // Ensure connection is open
        if (_connection.State != ConnectionState.Open)
        {
            _connection.Open();
        }

        // Check if user is blocked from ordering
        var userCheck = await _connection.QueryFirstOrDefaultAsync<dynamic>(
            @"SELECT 
                order_blocked_until AS OrderBlockedUntil
              FROM users WHERE id = @UserId",
            new { UserId = userId });

        if (userCheck != null && userCheck.OrderBlockedUntil != null)
        {
            var blockedUntil = (DateTime)userCheck.OrderBlockedUntil;
            if (blockedUntil > DateTime.UtcNow)
            {
                throw new OrderBlockedException(blockedUntil);
            }
        }

        // Get user info before transaction
        var user = await _connection.QueryFirstOrDefaultAsync<dynamic>(
            @"SELECT 
                email AS Email,
                name AS Name
              FROM users WHERE id = @UserId",
            new { UserId = userId });

        // Get cart and verify total
        var cart = await _cartService.GetCartAsync(userId, couponCode);
        
        if (cart.Items.Count == 0)
        {
            throw new CartValidationException("Cart is empty");
        }

        // Verify the amount matches cart total
        var expectedTotal = cart.Total;
        if (Math.Abs(amount - expectedTotal) > 0.01m)
        {
            throw new AmountMismatchException(expectedTotal, amount);
        }

        // Generate order code
        var orderCode = await _orderCodeService.GenerateOrderCodeAsync();

        // Create order
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            Id = orderId,
            OrderCode = orderCode,
            UserId = userId,
            TotalAmount = cart.Total,
            Status = OrderStatus.Pending,
            CouponId = cart.AppliedCoupon?.Id,
            StripePaymentIntentId = null, // No Stripe for COD
            ShippingAddressId = shippingAddressId,
            CreatedAt = DateTime.UtcNow
        };

        using var transaction = _connection.BeginTransaction();
        try
        {
            // Insert order
            await _connection.ExecuteAsync(@"
                INSERT INTO orders (id, order_code, user_id, total_amount, status, coupon_id, stripe_payment_intent_id, shipping_address_id, created_at)
                VALUES (@Id, @OrderCode, @UserId, @TotalAmount, @Status, @CouponId, @StripePaymentIntentId, @ShippingAddressId, @CreatedAt)",
                new
                {
                    order.Id,
                    OrderCode = order.OrderCode,
                    order.UserId,
                    TotalAmount = order.TotalAmount,
                    Status = order.Status.ToString(),
                    order.CouponId,
                    StripePaymentIntentId = (string?)null,
                    order.ShippingAddressId,
                    order.CreatedAt
                }, transaction);

            // Check for high volume order and notify admin
            if (order.TotalAmount >= 1000m || cart.Items.Count >= 20)
            {
                await _adminNotificationService.NotifyHighVolumeOrderAsync(
                    order.OrderCode,
                    order.TotalAmount,
                    cart.Items.Count);
            }

            // Insert order items
            const int MAX_QUANTITY_PER_PRODUCT = 10;
            foreach (var cartItem in cart.Items)
            {
                // Validate maximum quantity per product
                if (cartItem.Quantity > MAX_QUANTITY_PER_PRODUCT)
                {
                    throw new CartValidationException($"Maximum quantity allowed per product is {MAX_QUANTITY_PER_PRODUCT}. Product {cartItem.Product?.Name ?? cartItem.ProductId.ToString()} has quantity {cartItem.Quantity}.");
                }

                var orderItem = new OrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    ProductId = cartItem.ProductId,
                    Quantity = cartItem.Quantity,
                    Price = cartItem.Product != null ? cartItem.Product.DisplayPrice : 0
                };

                await _connection.ExecuteAsync(@"
                    INSERT INTO order_items (id, order_id, product_id, quantity, price, created_at)
                    VALUES (@Id, @OrderId, @ProductId, @Quantity, @Price, @CreatedAt)",
                    new
                    {
                        orderItem.Id,
                        OrderId = orderItem.OrderId,
                        ProductId = orderItem.ProductId,
                        orderItem.Quantity,
                        orderItem.Price,
                        CreatedAt = DateTime.UtcNow
                    }, transaction);

                // Update product stock
                await _connection.ExecuteAsync(
                    "UPDATE products SET available_quantity = available_quantity - @Quantity WHERE id = @ProductId",
                    new { Quantity = cartItem.Quantity, ProductId = cartItem.ProductId },
                    transaction);
            }

            // Create payment record for COD
            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                StripePaymentId = $"COD-{orderCode}", // Unique identifier for COD
                Amount = order.TotalAmount,
                Status = PaymentStatus.Pending, // Will be marked as completed when order is delivered
                PaymentMethod = "COD",
                Currency = currency,
                CreatedAt = DateTime.UtcNow
            };

            await _connection.ExecuteAsync(@"
                INSERT INTO payments (id, order_id, stripe_payment_id, amount, status, payment_method, currency, created_at)
                VALUES (@Id, @OrderId, @StripePaymentId, @Amount, @Status, @PaymentMethod, @Currency, @CreatedAt)",
                new
                {
                    payment.Id,
                    OrderId = payment.OrderId,
                    StripePaymentId = payment.StripePaymentId,
                    payment.Amount,
                    Status = payment.Status.ToString(),
                    payment.PaymentMethod,
                    payment.Currency,
                    payment.CreatedAt
                }, transaction);

            // Clear cart
            await _cartService.ClearCartAsync(userId);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        // Send order confirmation email (outside transaction)
        try
        {
            if (user != null)
            {
                var orderItems = cart.Items.Select(item => new OrderItemInfo(
                    item.Product?.Name ?? "Product",
                    item.Quantity,
                    item.Product?.Price ?? 0
                )).ToList();
                
                await _emailService.SendOrderConfirmationAsync(
                    user.Email,
                    orderCode,
                    order.TotalAmount,
                    orderItems);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send order confirmation email");
        }

        return new CodOrderResult(
            orderCode,
            orderId,
            "Order placed successfully. Payment will be collected on delivery.");
    }
}

