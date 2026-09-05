using ECommerce.Models;
using ECommerce.Utils;
using System.Data;
using Dapper;

namespace ECommerce.Services;

public class CartService : ICartService
{
    private readonly IDbConnection _connection;
    private readonly ICouponService _couponService;
    private readonly ILogger<CartService> _logger;
    private const decimal TAX_RATE = 0.10m;

    private const string PRODUCT_SELECT_COLUMNS = @"
        id AS Id,
        name AS Name,
        description AS Description,
        price AS Price,
        sale_price AS SalePrice,
        images AS Images,
        available_quantity AS AvailableQuantity,
        category AS Category,
        sku AS Sku,
        colors AS Colors,
        sizes AS Sizes,
        COALESCE(color_images::text, '{}') AS ColorImagesJson,
        customization_type AS CustomizationType,
        color_surcharge AS ColorSurcharge,
        no_surcharge_colors AS NoSurchargeColors,
        customization_policy AS CustomizationPolicy,
        image_object_position AS ImageObjectPosition,
        is_active AS IsActive,
        created_at AS CreatedAt,
        updated_at AS UpdatedAt";

    private const string CART_ITEM_SELECT = @"
        id AS Id,
        user_id AS UserId,
        product_id AS ProductId,
        quantity AS Quantity,
        selected_color AS SelectedColor,
        selected_size AS SelectedSize,
        custom_number AS CustomNumber,
        writing_color AS WritingColor,
        created_at AS CreatedAt,
        updated_at AS UpdatedAt";

    public CartService(IDbConnection connection, ICouponService couponService, ILogger<CartService> logger)
    {
        _connection = connection;
        _couponService = couponService;
        _logger = logger;
    }

    public async Task<CartItem> AddToCartAsync(
        Guid userId,
        Guid productId,
        int quantity,
        string? selectedColor = null,
        string? selectedSize = null,
        string? customNumber = null,
        string? writingColor = null)
    {
        const int MAX_QUANTITY_PER_PRODUCT = 10;
        if (quantity > MAX_QUANTITY_PER_PRODUCT)
        {
            throw new CartValidationException($"Maximum quantity allowed per product is {MAX_QUANTITY_PER_PRODUCT}. You requested {quantity}.");
        }

        var product = await _connection.QueryFirstOrDefaultAsync<Product>(
            $"SELECT {PRODUCT_SELECT_COLUMNS} FROM products WHERE id = @ProductId AND is_active = TRUE",
            new { ProductId = productId });

        if (product == null)
        {
            throw new ProductNotFoundException(productId.ToString());
        }

        product.HydrateColorImages();

        var normalizedNumber = ProductCustomizationRules.NormalizeNumber(customNumber);
        var normalizedWriting = ProductCustomizationRules.NormalizeWritingColor(product, writingColor);
        ProductCustomizationRules.ValidateForCart(product, selectedColor, normalizedNumber, normalizedWriting);

        if (product.AvailableQuantity < quantity)
        {
            throw new InsufficientStockException(product.Name, product.AvailableQuantity);
        }

        if (!string.IsNullOrEmpty(selectedColor) && product.Colors.Count > 0
            && !product.Colors.Any(c => c.Equals(selectedColor, StringComparison.OrdinalIgnoreCase)))
        {
            throw new CartValidationException($"Color '{selectedColor}' is not available for this product.");
        }

        if (!string.IsNullOrWhiteSpace(selectedSize) && product.Sizes.Count > 0
            && !product.Sizes.Any(s => s.Equals(selectedSize.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw new CartValidationException($"Size '{selectedSize}' is not available for this product.");
        }

        var existingItem = await _connection.QueryFirstOrDefaultAsync<CartItem>(
            $@"SELECT {CART_ITEM_SELECT}
              FROM cart_items
              WHERE user_id = @UserId AND product_id = @ProductId
                AND COALESCE(selected_color, '') = COALESCE(@SelectedColor, '')
                AND COALESCE(selected_size, '') = COALESCE(@SelectedSize, '')
                AND COALESCE(custom_number, '') = COALESCE(@CustomNumber, '')
                AND COALESCE(writing_color, '') = COALESCE(@WritingColor, '')",
            new
            {
                UserId = userId,
                ProductId = productId,
                SelectedColor = selectedColor,
                SelectedSize = selectedSize?.Trim(),
                CustomNumber = normalizedNumber,
                WritingColor = normalizedWriting
            });

        if (existingItem != null)
        {
            await _connection.ExecuteAsync(
                @"UPDATE cart_items SET quantity = @Quantity, updated_at = CURRENT_TIMESTAMP WHERE id = @Id",
                new { Quantity = quantity, Id = existingItem.Id });

            existingItem.Quantity = quantity;
            existingItem.UpdatedAt = DateTime.UtcNow;
            return existingItem;
        }

        var cartItem = new CartItem
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProductId = productId,
            Quantity = quantity,
            SelectedColor = selectedColor,
            SelectedSize = selectedSize?.Trim(),
            CustomNumber = normalizedNumber,
            WritingColor = normalizedWriting,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _connection.ExecuteAsync(@"
            INSERT INTO cart_items (id, user_id, product_id, quantity, selected_color, selected_size, custom_number, writing_color, created_at, updated_at)
            VALUES (@Id, @UserId, @ProductId, @Quantity, @SelectedColor, @SelectedSize, @CustomNumber, @WritingColor, @CreatedAt, @UpdatedAt)",
            cartItem);

        _logger.LogInformation("Added product {ProductId} to cart for user {UserId}", productId, userId);
        return cartItem;
    }

    public async Task<bool> RemoveFromCartAsync(Guid userId, Guid itemId)
    {
        var affected = await _connection.ExecuteAsync(
            "DELETE FROM cart_items WHERE id = @ItemId AND user_id = @UserId",
            new { ItemId = itemId, UserId = userId });

        if (affected > 0)
        {
            _logger.LogInformation("Removed cart item {ItemId} for user {UserId}", itemId, userId);
        }

        return affected > 0;
    }

    public async Task<CartSummary> GetCartAsync(Guid userId, string? couponCode = null)
    {
        var items = await _connection.QueryAsync<CartItem>(
            $@"SELECT {CART_ITEM_SELECT}
              FROM cart_items WHERE user_id = @UserId ORDER BY created_at",
            new { UserId = userId });

        var cartItems = items.ToList();

        if (cartItems.Count > 0)
        {
            var productIds = cartItems.Select(i => i.ProductId).Distinct().ToArray();
            var products = (await _connection.QueryAsync<Product>(
                $"SELECT {PRODUCT_SELECT_COLUMNS} FROM products WHERE id = ANY(@ProductIds) AND is_active = TRUE",
                new { ProductIds = productIds })).ToList();
            foreach (var p in products)
            {
                p.HydrateColorImages();
            }

            var productLookup = products.ToDictionary(p => p.Id, p => p);
            foreach (var item in cartItems)
            {
                if (productLookup.TryGetValue(item.ProductId, out var product))
                {
                    item.Product = product;
                }
            }
        }

        var summary = new CartSummary { Items = cartItems };

        summary.Subtotal = cartItems.Sum(item =>
            (item.Product != null
                ? ProductPricingRules.ResolveUnitPrice(item.Product, item.SelectedColor)
                : 0) * item.Quantity);

        summary.Tax = await CalculateTaxAsync(summary.Subtotal);

        if (!string.IsNullOrEmpty(couponCode))
        {
            try
            {
                var discount = await _couponService.ApplyCouponAsync(couponCode, summary.Subtotal);
                summary.Discount = summary.Subtotal - discount;
                summary.AppliedCoupon = await _connection.QueryFirstOrDefaultAsync<Coupon>(
                    @"SELECT 
                        id AS Id,
                        code AS Code,
                        discount_type AS DiscountType,
                        discount_value AS DiscountValue,
                        expiry_date AS ExpiryDate,
                        usage_limit AS UsageLimit,
                        usage_count AS UsageCount,
                        is_active AS IsActive,
                        min_purchase_amount AS MinPurchaseAmount,
                        max_discount_amount AS MaxDiscountAmount,
                        created_at AS CreatedAt
                      FROM coupons WHERE UPPER(code) = UPPER(@Code) AND is_active = TRUE",
                    new { Code = couponCode });
            }
            catch
            {
                // Coupon invalid, keep original totals
            }
        }

        summary.Total = summary.Subtotal - summary.Discount + summary.Tax;
        return summary;
    }

    public async Task<bool> UpdateCartItemQuantityAsync(Guid userId, Guid itemId, int quantity)
    {
        const int MAX_QUANTITY_PER_PRODUCT = 10;
        if (quantity > MAX_QUANTITY_PER_PRODUCT)
        {
            throw new CartValidationException($"Maximum quantity allowed per product is {MAX_QUANTITY_PER_PRODUCT}.");
        }

        if (quantity <= 0)
        {
            return await RemoveFromCartAsync(userId, itemId);
        }

        var affected = await _connection.ExecuteAsync(
            @"UPDATE cart_items SET quantity = @Quantity, updated_at = CURRENT_TIMESTAMP 
              WHERE id = @ItemId AND user_id = @UserId",
            new { Quantity = quantity, ItemId = itemId, UserId = userId });

        return affected > 0;
    }

    public async Task<bool> ClearCartAsync(Guid userId)
    {
        var affected = await _connection.ExecuteAsync(
            "DELETE FROM cart_items WHERE user_id = @UserId",
            new { UserId = userId });
        return affected >= 0;
    }

    public async Task<CartSummary> ApplyCouponAsync(Guid userId, string couponCode)
    {
        return await GetCartAsync(userId, couponCode);
    }

    public Task<decimal> CalculateTaxAsync(decimal subtotal)
    {
        return Task.FromResult(subtotal * TAX_RATE);
    }
}
