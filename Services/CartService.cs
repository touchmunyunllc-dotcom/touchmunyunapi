using ECommerce.Data;
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
    private const decimal TAX_RATE = 0.10m; // 10% tax

    // Explicit column mapping for proper Dapper mapping from snake_case to PascalCase
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
        is_active AS IsActive,
        created_at AS CreatedAt,
        updated_at AS UpdatedAt";

    public CartService(IDbConnection connection, ICouponService couponService, ILogger<CartService> logger)
    {
        _connection = connection;
        _couponService = couponService;
        _logger = logger;
    }

    public async Task<CartItem> AddToCartAsync(Guid userId, Guid productId, int quantity, string? selectedColor = null, int? selectedSize = null)
    {
        // Validate maximum quantity per product (10)
        const int MAX_QUANTITY_PER_PRODUCT = 10;
        if (quantity > MAX_QUANTITY_PER_PRODUCT)
        {
            throw new CartValidationException($"Maximum quantity allowed per product is {MAX_QUANTITY_PER_PRODUCT}. You requested {quantity}.");
        }

        // Validate size range (1-100)
        if (selectedSize.HasValue && (selectedSize.Value < 1 || selectedSize.Value > 100))
        {
            throw new CartValidationException("Size must be between 1 and 100.");
        }

        // Check if item already exists in cart
        var existingItem = await _connection.QueryFirstOrDefaultAsync<CartItem>(
            @"SELECT 
                id AS Id,
                user_id AS UserId,
                product_id AS ProductId,
                quantity AS Quantity,
                selected_color AS SelectedColor,
                selected_size AS SelectedSize,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
              FROM cart_items WHERE user_id = @UserId AND product_id = @ProductId",
            new { UserId = userId, ProductId = productId });

        if (existingItem != null)
        {
            // Update quantity and attributes
            if (quantity > MAX_QUANTITY_PER_PRODUCT)
            {
                throw new CartValidationException($"Maximum quantity allowed per product is {MAX_QUANTITY_PER_PRODUCT}. You requested {quantity}.");
            }

            await _connection.ExecuteAsync(
                "UPDATE cart_items SET quantity = @Quantity, selected_color = @SelectedColor, selected_size = @SelectedSize, updated_at = CURRENT_TIMESTAMP WHERE id = @Id",
                new { Quantity = quantity, SelectedColor = selectedColor, SelectedSize = selectedSize, Id = existingItem.Id });
            
            existingItem.Quantity = quantity;
            existingItem.SelectedColor = selectedColor;
            existingItem.SelectedSize = selectedSize;
            existingItem.UpdatedAt = DateTime.UtcNow;
            return existingItem;
        }

        // Verify product exists and has stock
        var product = await _connection.QueryFirstOrDefaultAsync<Product>(
            $"SELECT {PRODUCT_SELECT_COLUMNS} FROM products WHERE id = @ProductId AND is_active = TRUE",
            new { ProductId = productId });

        if (product == null)
        {
            throw new ProductNotFoundException(productId.ToString());
        }

        if (product.AvailableQuantity < quantity)
        {
            throw new InsufficientStockException(product.Name, product.AvailableQuantity);
        }

        // Validate selected color is available for this product
        if (!string.IsNullOrEmpty(selectedColor) && product.Colors.Count > 0 && !product.Colors.Contains(selectedColor))
        {
            throw new CartValidationException($"Color '{selectedColor}' is not available for this product.");
        }

        // Validate selected size is available for this product
        if (selectedSize.HasValue && product.Sizes.Count > 0 && !product.Sizes.Contains(selectedSize.Value))
        {
            throw new CartValidationException($"Size '{selectedSize}' is not available for this product.");
        }

        // Add new cart item
        var cartItem = new CartItem
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProductId = productId,
            Quantity = quantity,
            SelectedColor = selectedColor,
            SelectedSize = selectedSize,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _connection.ExecuteAsync(@"
            INSERT INTO cart_items (id, user_id, product_id, quantity, selected_color, selected_size, created_at, updated_at)
            VALUES (@Id, @UserId, @ProductId, @Quantity, @SelectedColor, @SelectedSize, @CreatedAt, @UpdatedAt)",
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
            @"SELECT 
                id AS Id,
                user_id AS UserId,
                product_id AS ProductId,
                quantity AS Quantity,
                selected_color AS SelectedColor,
                selected_size AS SelectedSize,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
              FROM cart_items WHERE user_id = @UserId ORDER BY created_at",
            new { UserId = userId });

        var cartItems = items.ToList();

        if (cartItems.Count > 0)
        {
            var productIds = cartItems.Select(i => i.ProductId).Distinct().ToArray();
            var products = (await _connection.QueryAsync<Product>(
                $"SELECT {PRODUCT_SELECT_COLUMNS} FROM products WHERE id = ANY(@ProductIds) AND is_active = TRUE",
                new { ProductIds = productIds })).ToList();

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

        // Calculate subtotal
        summary.Subtotal = cartItems.Sum(item => 
            (item.Product?.DisplayPrice ?? 0) * item.Quantity);

        // Calculate tax
        summary.Tax = await CalculateTaxAsync(summary.Subtotal);

        // Apply coupon if provided
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
                      FROM coupons WHERE code = @Code",
                    new { Code = couponCode });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to apply coupon {CouponCode}", couponCode);
            }
        }

        summary.Total = summary.Subtotal + summary.Tax - summary.Discount;

        return summary;
    }

    public async Task<bool> UpdateCartItemQuantityAsync(Guid userId, Guid itemId, int quantity)
    {
        if (quantity <= 0)
        {
            return await RemoveFromCartAsync(userId, itemId);
        }

        // Verify stock availability
        var item = await _connection.QueryFirstOrDefaultAsync<CartItem>(
            @"SELECT 
                id AS Id,
                user_id AS UserId,
                product_id AS ProductId,
                quantity AS Quantity,
                selected_color AS SelectedColor,
                selected_size AS SelectedSize,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
              FROM cart_items WHERE id = @ItemId AND user_id = @UserId",
            new { ItemId = itemId, UserId = userId });

        if (item == null)
        {
            return false;
        }

        var product = await _connection.QueryFirstOrDefaultAsync<Product>(
            $"SELECT {PRODUCT_SELECT_COLUMNS} FROM products WHERE id = @ProductId",
            new { ProductId = item.ProductId });

        if (product == null || product.AvailableQuantity < quantity)
        {
            throw new InsufficientStockException(product?.Name ?? "Unknown", product?.AvailableQuantity ?? 0);
        }

        var affected = await _connection.ExecuteAsync(
            "UPDATE cart_items SET quantity = @Quantity, updated_at = CURRENT_TIMESTAMP WHERE id = @ItemId AND user_id = @UserId",
            new { Quantity = quantity, ItemId = itemId, UserId = userId });

        return affected > 0;
    }

    public async Task<bool> ClearCartAsync(Guid userId)
    {
        var affected = await _connection.ExecuteAsync(
            "DELETE FROM cart_items WHERE user_id = @UserId",
            new { UserId = userId });

        _logger.LogInformation("Cleared cart for user {UserId}", userId);
        return affected > 0;
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

