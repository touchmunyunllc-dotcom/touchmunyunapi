using ECommerce.DTOs;
using ECommerce.Models;
using ECommerce.Utils;
using System.Data;
using Dapper;

namespace ECommerce.Services;

public class GuestService : IGuestService
{
    private readonly IDbConnection _connection;
    private readonly IStripeService _stripeService;
    private readonly IOrderCodeService _orderCodeService;
    private readonly ICouponService _couponService;
    private readonly ICartService _cartService;
    private readonly IProductService _productService;
    private readonly IStripeCheckoutFulfillmentService _stripeCheckoutFulfillment;

    public GuestService(
        IDbConnection connection,
        IStripeService stripeService,
        IOrderCodeService orderCodeService,
        ICouponService couponService,
        ICartService cartService,
        IProductService productService,
        IStripeCheckoutFulfillmentService stripeCheckoutFulfillment)
    {
        _connection = connection;
        _stripeService = stripeService;
        _orderCodeService = orderCodeService;
        _couponService = couponService;
        _cartService = cartService;
        _productService = productService;
        _stripeCheckoutFulfillment = stripeCheckoutFulfillment;
    }

    public async Task<GuestCheckoutPreviewResponse> PreviewGuestCheckoutAsync(
        List<GuestOrderItemRequest> items,
        string? couponCode)
    {
        var guestItems = items
            .Select(i => new GuestOrderItem(i.ProductId, i.Name, i.Price, i.Quantity))
            .ToList();
        var (subtotal, tax, total, couponId) = await ComputeGuestTotalsAsync(guestItems, couponCode);
        return new GuestCheckoutPreviewResponse(subtotal, tax, total, couponId.HasValue);
    }

    /// <summary>Subtotal, tax-on-original-subtotal, final total, coupon id if applied.</summary>
    private async Task<(decimal Subtotal, decimal Tax, decimal Total, Guid? CouponId)> ComputeGuestTotalsAsync(
        List<GuestOrderItem> items,
        string? couponCode)
    {
        decimal subtotal = items.Sum(item => item.Price * item.Quantity);
        decimal tax = await _cartService.CalculateTaxAsync(subtotal);
        decimal total = subtotal + tax;
        Guid? couponId = null;

        if (!string.IsNullOrEmpty(couponCode))
        {
            try
            {
                var discountedSubtotal = await _couponService.ApplyCouponAsync(couponCode, subtotal);
                total = discountedSubtotal + tax;
                couponId = await _connection.QueryFirstOrDefaultAsync<Guid?>(
                    "SELECT id FROM coupons WHERE UPPER(code) = UPPER(@Code) AND is_active = TRUE",
                    new { Code = couponCode });
            }
            catch
            {
                // Coupon invalid, keep original total
            }
        }

        return (subtotal, tax, total, couponId);
    }

    public async Task<GuestOrderResult> CreateGuestOrderAsync(
        string email,
        string name,
        List<GuestOrderItem> items,
        decimal totalAmount,
        string currency,
        string? couponCode,
        GuestAddress shippingAddress)
    {
        var (_, _, total, couponId) = await ComputeGuestTotalsAsync(items, couponCode);

        if (Math.Abs(totalAmount - total) > 0.01m)
        {
            throw new AmountMismatchException(total, totalAmount);
        }

        foreach (var item in items)
        {
            var product = await _productService.GetProductByIdAsync(item.ProductId);
            if (product == null || !product.IsActive)
            {
                throw new ProductNotFoundException(item.ProductId.ToString());
            }

            if (product.AvailableQuantity < item.Quantity)
            {
                throw new InsufficientStockException(product.Name, product.AvailableQuantity);
            }
        }

        var paymentIntent = await _stripeService.CreatePaymentIntentAsync(total, currency);

        var payload = new StripeCheckoutPendingPayload
        {
            Kind = StripeCheckoutPendingPayload.KindGuest,
            GuestEmail = email.Trim(),
            GuestName = name.Trim(),
            TotalAmount = total,
            Currency = currency,
            CouponId = couponId,
            GuestAddress = new GuestPendingAddress
            {
                Line1 = shippingAddress.AddressLine1,
                Line2 = shippingAddress.AddressLine2,
                City = shippingAddress.City,
                State = shippingAddress.State,
                PostalCode = shippingAddress.PostalCode,
                Country = shippingAddress.Country
            },
            Items = items.Select(i => new PendingCheckoutLineItem
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                UnitPrice = i.Price
            }).ToList()
        };

        await _stripeCheckoutFulfillment.SavePendingCheckoutAsync(paymentIntent.PaymentIntentId, payload);

        return new GuestOrderResult(
            null,
            paymentIntent.ClientSecret,
            paymentIntent.PaymentIntentId,
            null);
    }

    public async Task<string?> GetOrderCodeByPaymentIntentAsync(string paymentIntentId)
    {
        return await _connection.QueryFirstOrDefaultAsync<string>(
            "SELECT order_code FROM orders WHERE stripe_payment_intent_id = @Pi",
            new { Pi = paymentIntentId });
    }

    public async Task<GuestOrder?> GetGuestOrderAsync(string orderCode)
    {
        var order = await _connection.QueryFirstOrDefaultAsync(
            @"SELECT * FROM orders WHERE order_code = @OrderCode",
            new { OrderCode = orderCode });

        if (order == null)
        {
            return null;
        }

        var orderItems = await _connection.QueryAsync<OrderItem>(
            "SELECT * FROM order_items WHERE order_id = @OrderId",
            new { OrderId = order.id });

        foreach (var item in orderItems)
        {
            item.Product = await _productService.GetProductByIdAsync(item.ProductId);
        }

        return new GuestOrder(
            order.order_code,
            order.guest_email,
            order.total_amount,
            order.status,
            order.tracking_number,
            order.tracking_url,
            orderItems.ToList());
    }

    public async Task<OrderTrackingInfo?> TrackGuestOrderAsync(string orderCode)
    {
        var order = await _connection.QueryFirstOrDefaultAsync(
            @"SELECT order_code, status, tracking_number, tracking_url, created_at, updated_at
              FROM orders
              WHERE order_code = @OrderCode",
            new { OrderCode = orderCode });

        if (order == null)
        {
            return null;
        }

        return new OrderTrackingInfo(
            order.order_code,
            order.status,
            order.tracking_number,
            order.tracking_url,
            order.created_at,
            order.updated_at);
    }
}
