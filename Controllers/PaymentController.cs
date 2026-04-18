using ECommerce.DTOs;
using ECommerce.Models;
using ECommerce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Security.Claims;
using System.Text.Json;
using Dapper;
using Stripe;
using Stripe.Checkout;
using Microsoft.Extensions.Logging;

namespace ECommerce.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IDbConnection _connection;
    private readonly IStripeService _stripeService;
    private readonly IStripeCheckoutFulfillmentService _stripeCheckoutFulfillment;
    private readonly IOrderService _orderService;
    private readonly IAdminNotificationService _adminNotificationService;
    private readonly IEmailService _emailService;
    private readonly ISMSService _smsService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymentsController> _logger;

    private static readonly JsonSerializerOptions PendingJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public PaymentsController(
        IPaymentService paymentService,
        IDbConnection connection,
        IStripeService stripeService,
        IStripeCheckoutFulfillmentService stripeCheckoutFulfillment,
        IOrderService orderService,
        IAdminNotificationService adminNotificationService,
        IEmailService emailService,
        ISMSService smsService,
        IConfiguration configuration,
        ILogger<PaymentsController> logger)
    {
        _paymentService = paymentService;
        _connection = connection;
        _stripeService = stripeService;
        _stripeCheckoutFulfillment = stripeCheckoutFulfillment;
        _orderService = orderService;
        _adminNotificationService = adminNotificationService;
        _emailService = emailService;
        _smsService = smsService;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("create-cod-order")]
    [Authorize]
    [ProducesResponseType(typeof(CodOrderResponse), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CreateCodOrder([FromBody] CreateCodOrderRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized();
        }

        try
        {
            var result = await _paymentService.CreateCodOrderAsync(
                Guid.Parse(userId),
                request.Amount,
                request.Currency,
                request.CouponCode,
                request.ShippingAddressId);

            return Ok(new CodOrderResponse(
                result.OrderCode,
                result.OrderId,
                result.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating COD order");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("create-intent")]
    [Authorize]
    [ProducesResponseType(typeof(PaymentIntentResponse), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CreatePaymentIntent([FromBody] CreatePaymentIntentRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized();
        }

        try
        {
            var result = await _paymentService.CreatePaymentIntentWithOrderAsync(
                Guid.Parse(userId),
                request.Amount,
                request.Currency,
                request.CouponCode,
                request.ShippingAddressId);

            return Ok(new PaymentIntentResponse(
                result.ClientSecret,
                result.PaymentIntentId,
                result.OrderId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating payment intent");
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// After Elements confirms payment, finalizes order if webhook has not run yet (idempotent).
    /// </summary>
    [HttpPost("confirm-stripe-checkout")]
    [Authorize]
    [ProducesResponseType(typeof(ConfirmStripeCheckoutResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ConfirmStripeCheckout([FromBody] ConfirmStripeCheckoutRequest request)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null || !Guid.TryParse(userIdStr, out var userId))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.PaymentIntentId))
        {
            return BadRequest(new { message = "PaymentIntentId is required." });
        }

        var piId = request.PaymentIntentId.Trim();

        try
        {
            var existing = await _orderService.GetOrderByStripePaymentIntentAsync(userId, piId);
            if (existing != null)
            {
                return Ok(new ConfirmStripeCheckoutResponse(existing.Id, existing.OrderCode));
            }

            var pendingJson = await _connection.QueryFirstOrDefaultAsync<string>(
                "SELECT payload_json FROM stripe_checkout_pending WHERE payment_intent_id = @Pi",
                new { Pi = piId });

            if (string.IsNullOrEmpty(pendingJson))
            {
                return NotFound(new { message = "Checkout session not found or already completed." });
            }

            var payload = JsonSerializer.Deserialize<StripeCheckoutPendingPayload>(pendingJson, PendingJsonOptions);
            if (payload is not { Kind: StripeCheckoutPendingPayload.KindRegistered } ||
                payload.UserId != userId)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { message = "This payment does not belong to the current user." });
            }

            var pi = await _stripeService.GetPaymentIntentAsync(piId);
            if (pi.Status != "succeeded")
            {
                return BadRequest(new { message = "Payment has not succeeded yet. Please wait or refresh." });
            }

            var result = await _stripeCheckoutFulfillment.TryFulfillPaymentIntentAsync(
                pi.Id,
                pi.Amount,
                pi.Currency);

            if (!result.Ok)
            {
                _logger.LogWarning(
                    "confirm-stripe-checkout could not fulfill PI {Pi}: {Failure}",
                    piId, result.Failure);
                return BadRequest(new { message = "Unable to finalize order.", failure = result.Failure.ToString() });
            }

            if (!result.OrderId.HasValue)
            {
                return NotFound(new { message = "Order not found after fulfillment." });
            }

            var order = await _orderService.GetOrderByIdAsync(result.OrderId.Value, userId);
            if (order == null)
            {
                return NotFound(new { message = "Order not found." });
            }

            if (result.NewlyCreated)
            {
                await SendStripeOrderNotificationsAsync(order.Id, pi.Id);
            }

            return Ok(new ConfirmStripeCheckoutResponse(order.Id, order.OrderCode));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "confirm-stripe-checkout failed for PI {Pi}", piId);
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Stripe Checkout hosted session (redirect). Cart and total are computed server-side.</summary>
    [HttpPost("create-checkout-session")]
    [Authorize]
    [ProducesResponseType(typeof(CheckoutSessionResponse), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CreateCheckoutSession([FromBody] CreateCheckoutSessionRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized();
        }

        try
        {
            var uid = Guid.Parse(userId);
            var currency = string.IsNullOrWhiteSpace(request.Currency) ? "usd" : request.Currency!.Trim().ToLowerInvariant();
            var payload = await _paymentService.BuildRegisteredCheckoutPayloadAsync(
                uid,
                request.CouponCode,
                request.ShippingAddressId,
                currency);

            var result = await _stripeService.CreateCheckoutSessionForTotalAsync(
                payload.TotalAmount,
                payload.Currency,
                userId);

            await _stripeCheckoutFulfillment.SaveHostedCheckoutPendingAsync(result.SessionId, payload);

            return Ok(new CheckoutSessionResponse(result.SessionId, result.Url));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Stripe Checkout session");
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>After returning from Stripe Hosted Checkout with ?session_id=…, resolves PI + order (poll until paid).</summary>
    [HttpGet("resolve-checkout-session")]
    [Authorize]
    [ProducesResponseType(typeof(ResolveCheckoutSessionResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> ResolveCheckoutSession([FromQuery] string sessionId)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null || !Guid.TryParse(userIdStr, out var userId))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return BadRequest(new { message = "sessionId is required." });
        }

        try
        {
            var session = await _stripeService.GetCheckoutSessionAsync(sessionId.Trim());
            if (!string.Equals(session.ClientReferenceId, userIdStr, StringComparison.Ordinal))
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { message = "This checkout session does not belong to the current user." });
            }

            await _stripeCheckoutFulfillment.TryMigrateHostedStripeCheckoutPendingAsync(session);

            if (string.IsNullOrEmpty(session.PaymentIntentId))
            {
                return Ok(new ResolveCheckoutSessionResponse(null, null, null, false));
            }

            var pi = await _stripeService.GetPaymentIntentAsync(session.PaymentIntentId);
            if (pi.Status == "succeeded")
            {
                var fulfill = await _stripeCheckoutFulfillment.TryFulfillPaymentIntentAsync(
                    pi.Id,
                    pi.Amount,
                    pi.Currency);

                if (fulfill.Ok && fulfill.NewlyCreated && fulfill.OrderId.HasValue)
                {
                    await SendStripeOrderNotificationsAsync(fulfill.OrderId.Value, pi.Id);
                }
                else if (!fulfill.Ok)
                {
                    _logger.LogWarning(
                        "resolve-checkout-session fulfill failed for PI {Pi}: {Failure}",
                        pi.Id, fulfill.Failure);
                }
            }

            var order = await _orderService.GetOrderByStripePaymentIntentAsync(userId, session.PaymentIntentId);
            var paid = string.Equals(session.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase)
                       || pi.Status == "succeeded";

            return Ok(new ResolveCheckoutSessionResponse(
                session.PaymentIntentId,
                order?.Id,
                order?.OrderCode,
                paid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "resolve-checkout-session failed for session {SessionId}", sessionId);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> HandleStripeWebhook()
    {
        var json = await new StreamReader(Request.Body).ReadToEndAsync();
        var webhookSecret = _configuration["Stripe:WebhookSecret"];

        try
        {
            var stripeEvent = EventUtility.ConstructEvent(
                json,
                Request.Headers["Stripe-Signature"],
                webhookSecret);

            if (_connection.State != ConnectionState.Open)
            {
                _connection.Open();
            }

            // Stripe retries use the same event id — claim once so side effects (emails, DB) are not duplicated.
            var claimedRows = await _connection.ExecuteAsync(
                @"INSERT INTO stripe_webhook_events (id, event_type) VALUES (@Id, @Type)
                  ON CONFLICT (id) DO NOTHING",
                new { Id = stripeEvent.Id, Type = stripeEvent.Type });

            if (claimedRows == 0)
            {
                _logger.LogInformation(
                    "Ignoring duplicate Stripe webhook {EventId} ({EventType})",
                    stripeEvent.Id,
                    stripeEvent.Type);
                return Ok(new { received = true, duplicate = true });
            }

            try
            {
                if (stripeEvent.Type == Events.PaymentIntentSucceeded)
                {
                    var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
                    if (paymentIntent != null)
                    {
                        // Hosted Checkout may attach PI before checkout.session.completed migrates the snapshot.
                        await _stripeCheckoutFulfillment.TryMigrateHostedStripeCheckoutPendingByPaymentIntentAsync(
                            paymentIntent.Id);

                        var fulfill = await _stripeCheckoutFulfillment.TryFulfillPaymentIntentAsync(
                            paymentIntent.Id,
                            paymentIntent.Amount,
                            paymentIntent.Currency);

                        if (fulfill.Ok && fulfill.NewlyCreated && fulfill.OrderId.HasValue)
                        {
                            await SendStripeOrderNotificationsAsync(fulfill.OrderId.Value, paymentIntent.Id);
                        }
                        else if (!fulfill.Ok && fulfill.Failure == FulfillmentFailureKind.NoPendingCheckout)
                        {
                            await TryLegacyStripePaymentSuccessAsync(paymentIntent);
                        }
                    }
                }
                else if (stripeEvent.Type == Events.CheckoutSessionCompleted)
                {
                    var session = stripeEvent.Data.Object as Session;
                    if (session != null)
                    {
                        await _stripeCheckoutFulfillment.TryMigrateHostedStripeCheckoutPendingAsync(session);
                        if (!string.IsNullOrEmpty(session.PaymentIntentId))
                        {
                            var pi = await _stripeService.GetPaymentIntentAsync(session.PaymentIntentId);
                            if (pi.Status == "succeeded")
                            {
                                var fulfill = await _stripeCheckoutFulfillment.TryFulfillPaymentIntentAsync(
                                    pi.Id,
                                    pi.Amount,
                                    pi.Currency);

                                if (fulfill.Ok && fulfill.NewlyCreated && fulfill.OrderId.HasValue)
                                {
                                    await SendStripeOrderNotificationsAsync(fulfill.OrderId.Value, pi.Id);
                                }
                            }
                        }
                    }
                }
                else if (stripeEvent.Type == Events.CheckoutSessionExpired)
                {
                    var expiredSession = stripeEvent.Data.Object as Session;
                    if (expiredSession != null)
                    {
                        await _connection.ExecuteAsync(
                            "DELETE FROM stripe_hosted_checkout_pending WHERE session_id = @Sid",
                            new { Sid = expiredSession.Id });
                    }
                }
                else if (stripeEvent.Type == Events.PaymentIntentPaymentFailed)
                {
                    var paymentIntent = stripeEvent.Data.Object as PaymentIntent;

                    if (paymentIntent != null)
                    {
                        await _connection.ExecuteAsync(
                            "DELETE FROM stripe_checkout_pending WHERE payment_intent_id = @Pi",
                            new { Pi = paymentIntent.Id });

                        await _connection.ExecuteAsync(
                            "UPDATE payments SET status = @Status, updated_at = CURRENT_TIMESTAMP WHERE stripe_payment_id = @StripePaymentId",
                            new { Status = "Failed", StripePaymentId = paymentIntent.Id });

                        var order = await _connection.QueryFirstOrDefaultAsync(
                            @"SELECT o.*, u.email, o.guest_email, o.order_code
                              FROM orders o 
                              LEFT JOIN users u ON o.user_id = u.id 
                              WHERE o.stripe_payment_intent_id = @PaymentIntentId",
                            new { PaymentIntentId = paymentIntent.Id });

                        if (order != null)
                        {
                            try
                            {
                                var email = order.email ?? order.guest_email;
                                if (!string.IsNullOrEmpty(email))
                                {
                                    await _emailService.SendEmailAsync(
                                        email,
                                        "Payment Failed - Order Update",
                                        $"Your payment for order {order.order_code} has failed. Please try again or contact support.");
                                }
                            }
                            catch (Exception notifyEx)
                            {
                                _logger.LogWarning(notifyEx, "Payment-failed customer email skipped for PI {Pi}", paymentIntent.Id);
                            }

                            await _adminNotificationService.NotifyFailedPaymentAsync(
                                order.order_code,
                                paymentIntent.Id,
                                order.total_amount,
                                paymentIntent.LastPaymentError?.Message ?? "Payment failed");
                        }
                    }
                }
                else if (stripeEvent.Type == Events.PaymentIntentCanceled)
                {
                    var paymentIntent = stripeEvent.Data.Object as PaymentIntent;

                    if (paymentIntent != null)
                    {
                        await _connection.ExecuteAsync(
                            "DELETE FROM stripe_checkout_pending WHERE payment_intent_id = @Pi",
                            new { Pi = paymentIntent.Id });

                        await _connection.ExecuteAsync(
                            "UPDATE payments SET status = @Status, updated_at = CURRENT_TIMESTAMP WHERE stripe_payment_id = @StripePaymentId",
                            new { Status = "Cancelled", StripePaymentId = paymentIntent.Id });
                    }
                }
            }
            catch (Exception)
            {
                await _connection.ExecuteAsync(
                    "DELETE FROM stripe_webhook_events WHERE id = @Id",
                    new { Id = stripeEvent.Id });
                throw;
            }

            return Ok(new { received = true });
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe webhook error");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook");
            return BadRequest(new { message = "Error processing webhook" });
        }
    }

    /// <summary>Pre-deferred-checkout: order existed at PI creation with Pending payment.</summary>
    private async Task TryLegacyStripePaymentSuccessAsync(PaymentIntent paymentIntent)
    {
        await _connection.ExecuteAsync(
            "UPDATE payments SET status = @Status, updated_at = CURRENT_TIMESTAMP WHERE stripe_payment_id = @StripePaymentId AND status = @Pending",
            new { Status = "Completed", StripePaymentId = paymentIntent.Id, Pending = "Pending" });

        var order = await _connection.QueryFirstOrDefaultAsync(
            @"SELECT o.id, o.status AS order_status, o.order_code, o.total_amount, o.guest_email, u.email, u.name, u.phone_number
              FROM orders o 
              LEFT JOIN users u ON o.user_id = u.id 
              WHERE o.stripe_payment_intent_id = @PaymentIntentId",
            new { PaymentIntentId = paymentIntent.Id });

        if (order == null || (string)order.order_status == "Paid")
        {
            return;
        }

        await _connection.ExecuteAsync(
            "UPDATE orders SET status = @Status, updated_at = CURRENT_TIMESTAMP WHERE id = @Id",
            new { Status = "Paid", Id = order.id });

        await SendStripeOrderNotificationsAsync((Guid)order.id, paymentIntent.Id);
    }

    private async Task SendStripeOrderNotificationsAsync(Guid orderId, string paymentIntentId)
    {
        try
        {
            var order = await _connection.QueryFirstOrDefaultAsync(
                @"SELECT o.*, u.email, u.name, u.phone_number, o.guest_email, o.order_code
                  FROM orders o 
                  LEFT JOIN users u ON o.user_id = u.id 
                  WHERE o.id = @OrderId",
                new { OrderId = orderId });

            if (order == null)
            {
                return;
            }

            var orderItems = await _connection.QueryAsync(
                @"SELECT p.name, oi.quantity, oi.price 
                  FROM order_items oi 
                  INNER JOIN products p ON oi.product_id = p.id 
                  WHERE oi.order_id = @OrderId",
                new { OrderId = orderId });

            var items = orderItems.Select(item => new OrderItemInfo(
                (string)item.name,
                (int)item.quantity,
                (decimal)item.price
            )).ToList();

            var email = order.email ?? order.guest_email;
            if (!string.IsNullOrEmpty(email))
            {
                await _emailService.SendOrderConfirmationAsync(
                    email,
                    order.order_code ?? order.id.ToString(),
                    order.total_amount,
                    items);

                await _emailService.SendPaymentReceiptAsync(
                    email,
                    order.order_code ?? order.id.ToString(),
                    paymentIntentId,
                    order.total_amount,
                    DateTime.UtcNow);

                var smsRecipient = (string?)order.phone_number ?? email;
                await _smsService.SendOrderConfirmationAsync(
                    smsRecipient,
                    order.order_code ?? order.id.ToString(),
                    order.total_amount);

                await _smsService.SendOrderStatusUpdateAsync(
                    smsRecipient,
                    order.order_code ?? order.id.ToString(),
                    "Paid");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Post-payment notifications failed for order {OrderId} (PI {Pi}); webhook still recorded as processed",
                orderId, paymentIntentId);
        }
    }
}
