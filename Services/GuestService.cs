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
            .Select(i => new GuestOrderItem(
                i.ProductId, i.Name, i.Price, i.Quantity,
                i.SelectedColor, i.SelectedSize, i.CustomNumber, i.WritingColor))
            .ToList();
        var (subtotal, tax, total, couponId, _) = await ComputeGuestTotalsAsync(guestItems, couponCode, validate: true);
        return new GuestCheckoutPreviewResponse(subtotal, tax, total, couponId.HasValue);
    }

    /// <summary>Server-side prices from catalog; validates stock/customization when validate is true.</summary>
    private async Task<(decimal Subtotal, decimal Tax, decimal Total, Guid? CouponId, List<PendingCheckoutLineItem> Lines)> ComputeGuestTotalsAsync(
        List<GuestOrderItem> items,
        string? couponCode,
        bool validate)
    {
        if (items == null || items.Count == 0)
        {
            throw new CartValidationException("Cart is empty.");
        }

        decimal subtotal = 0;
        var lines = new List<PendingCheckoutLineItem>();

        foreach (var item in items)
        {
            var product = await _productService.GetProductByIdAsync(item.ProductId);
            if (product == null || !product.IsActive)
            {
                throw new ProductNotFoundException(item.ProductId.ToString());
            }

            if (validate)
            {
                if (product.AvailableQuantity < item.Quantity)
                {
                    throw new InsufficientStockException(product.Name, product.AvailableQuantity);
                }

                var normalizedNumber = ProductCustomizationRules.NormalizeNumber(item.CustomNumber);
                var normalizedWriting = ProductCustomizationRules.NormalizeWritingColor(product, item.WritingColor);
                ProductCustomizationRules.ValidateForCart(
                    product,
                    item.SelectedColor,
                    normalizedNumber,
                    normalizedWriting);

                if (!string.IsNullOrEmpty(item.SelectedColor) && product.Colors.Count > 0
                    && !product.Colors.Any(c => c.Equals(item.SelectedColor, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new CartValidationException($"Color '{item.SelectedColor}' is not available for this product.");
                }

                if (!string.IsNullOrWhiteSpace(item.SelectedSize) && product.Sizes.Count > 0
                    && !product.Sizes.Any(s => s.Equals(item.SelectedSize.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    throw new CartValidationException($"Size '{item.SelectedSize}' is not available for this product.");
                }
            }

            var unitPrice = ProductPricingRules.ResolveUnitPrice(product, item.SelectedColor);
            subtotal += unitPrice * item.Quantity;
            lines.Add(new PendingCheckoutLineItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = unitPrice,
                SelectedColor = item.SelectedColor,
                SelectedSize = item.SelectedSize?.Trim(),
                CustomNumber = ProductCustomizationRules.NormalizeNumber(item.CustomNumber),
                WritingColor = ProductCustomizationRules.NormalizeWritingColor(product, item.WritingColor)
            });
        }

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

        return (subtotal, tax, total, couponId, lines);
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
        var (_, _, total, couponId, lines) = await ComputeGuestTotalsAsync(items, couponCode, validate: true);

        if (Math.Abs(totalAmount - total) > 0.01m)
        {
            throw new AmountMismatchException(total, totalAmount);
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
            Items = lines
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

        var orderItems = (await _connection.QueryAsync<OrderItem>(
            @"SELECT
                id AS Id,
                order_id AS OrderId,
                product_id AS ProductId,
                quantity AS Quantity,
                price AS Price,
                selected_color AS SelectedColor,
                selected_size AS SelectedSize,
                custom_number AS CustomNumber,
                writing_color AS WritingColor
              FROM order_items WHERE order_id = @OrderId",
            new { OrderId = order.id })).ToList();

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
