using ECommerce.Models;
using ECommerce.Utils;
using System.Data;
using Dapper;

namespace ECommerce.Services;

public class OrderService : IOrderService
{
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

    private readonly IDbConnection _connection;
    private readonly IOrderCodeService _orderCodeService;
    private readonly IProductService _productService;

    public OrderService(
        IDbConnection connection,
        IOrderCodeService orderCodeService,
        IProductService productService)
    {
        _connection = connection;
        _orderCodeService = orderCodeService;
        _productService = productService;
    }

    public async Task<List<Order>> GetUserOrdersAsync(Guid userId, DateTime? startDate = null, DateTime? endDate = null, int limit = 5)
    {
        var sql = @"SELECT 
                id AS Id,
                order_code AS OrderCode,
                user_id AS UserId,
                guest_email AS GuestEmail,
                total_amount AS TotalAmount,
                status AS Status,
                coupon_id AS CouponId,
                shipping_address_id AS ShippingAddressId,
                stripe_payment_intent_id AS StripePaymentIntentId,
                tracking_number AS TrackingNumber,
                tracking_url AS TrackingUrl,
                notes AS Notes,
                cancellation_reason AS CancellationReason,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
              FROM orders WHERE user_id = @UserId";

        var parameters = new DynamicParameters();
        parameters.Add("UserId", userId);

        // Add date filters
        if (startDate.HasValue)
        {
            sql += " AND created_at >= @StartDate";
            parameters.Add("StartDate", startDate.Value);
        }

        if (endDate.HasValue)
        {
            sql += " AND created_at <= @EndDate";
            parameters.Add("EndDate", endDate.Value);
        }

        sql += " ORDER BY created_at DESC LIMIT @Limit";
        parameters.Add("Limit", limit);

        var orders = await _connection.QueryAsync<Order>(sql, parameters);

        var orderList = orders.ToList();

        await LoadOrderItemsAsync(orderList);

        return orderList;
    }

    public async Task<Order?> GetOrderByIdAsync(Guid orderId, Guid? userId = null)
    {
        var sql = @"SELECT 
                id AS Id,
                order_code AS OrderCode,
                user_id AS UserId,
                guest_email AS GuestEmail,
                total_amount AS TotalAmount,
                status AS Status,
                coupon_id AS CouponId,
                shipping_address_id AS ShippingAddressId,
                stripe_payment_intent_id AS StripePaymentIntentId,
                tracking_number AS TrackingNumber,
                tracking_url AS TrackingUrl,
                notes AS Notes,
                cancellation_reason AS CancellationReason,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
              FROM orders WHERE id = @Id";
        object parameters;

        if (userId.HasValue)
        {
            sql += " AND user_id = @UserId";
            parameters = new { Id = orderId, UserId = userId.Value };
        }
        else
        {
            parameters = new { Id = orderId };
        }

        var order = await _connection.QueryFirstOrDefaultAsync<Order>(sql, parameters);

        if (order != null)
        {
            await LoadOrderItemsAsync(new List<Order> { order });
        }

        return order;
    }

    public async Task<Order?> GetOrderByStripePaymentIntentAsync(Guid userId, string paymentIntentId)
    {
        var sql = @"SELECT 
                id AS Id,
                order_code AS OrderCode,
                user_id AS UserId,
                guest_email AS GuestEmail,
                total_amount AS TotalAmount,
                status AS Status,
                coupon_id AS CouponId,
                shipping_address_id AS ShippingAddressId,
                stripe_payment_intent_id AS StripePaymentIntentId,
                tracking_number AS TrackingNumber,
                tracking_url AS TrackingUrl,
                notes AS Notes,
                cancellation_reason AS CancellationReason,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
              FROM orders
              WHERE stripe_payment_intent_id = @PaymentIntentId AND user_id = @UserId";

        var order = await _connection.QueryFirstOrDefaultAsync<Order>(sql,
            new { PaymentIntentId = paymentIntentId, UserId = userId });

        if (order != null)
        {
            await LoadOrderItemsAsync(new List<Order> { order });
        }

        return order;
    }

    public async Task<Order?> GetOrderByCodeAsync(string orderCode, Guid? userId = null)
    {
        var sql = @"SELECT 
                id AS Id,
                order_code AS OrderCode,
                user_id AS UserId,
                guest_email AS GuestEmail,
                total_amount AS TotalAmount,
                status AS Status,
                coupon_id AS CouponId,
                shipping_address_id AS ShippingAddressId,
                stripe_payment_intent_id AS StripePaymentIntentId,
                tracking_number AS TrackingNumber,
                tracking_url AS TrackingUrl,
                notes AS Notes,
                cancellation_reason AS CancellationReason,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
              FROM orders WHERE order_code = @OrderCode";
        object parameters;

        if (userId.HasValue)
        {
            sql += " AND user_id = @UserId";
            parameters = new { OrderCode = orderCode, UserId = userId.Value };
        }
        else
        {
            parameters = new { OrderCode = orderCode };
        }

        var order = await _connection.QueryFirstOrDefaultAsync<Order>(sql, parameters);

        if (order != null)
        {
            await LoadOrderItemsAsync(new List<Order> { order });
        }

        return order;
    }

    public async Task<Order> CreateOrderAsync(
        Guid userId,
        Guid? shippingAddressId,
        List<OrderItemRequest> items)
    {
        // Check if user is blocked from ordering
        var user = await _connection.QueryFirstOrDefaultAsync<dynamic>(
            @"SELECT 
                order_blocked_until AS OrderBlockedUntil
              FROM users WHERE id = @UserId",
            new { UserId = userId });

        if (user != null && user.OrderBlockedUntil != null)
        {
            var blockedUntil = (DateTime)user.OrderBlockedUntil;
            if (blockedUntil > DateTime.UtcNow)
            {
                throw new OrderBlockedException(blockedUntil);
            }
        }

        // Generate order code
        var orderCode = await _orderCodeService.GenerateOrderCodeAsync();

        var orderId = Guid.NewGuid();
        var order = new Order
        {
            Id = orderId,
            OrderCode = orderCode,
            UserId = userId,
            Status = OrderStatus.Pending,
            ShippingAddressId = shippingAddressId,
            CreatedAt = DateTime.UtcNow,
            OrderItems = new List<OrderItem>()
        };

        decimal total = 0;

        using var transaction = _connection.BeginTransaction();
        try
        {
            foreach (var item in items)
            {
                var product = await _productService.GetProductByIdAsync(item.ProductId);
                if (product == null || !product.IsActive)
                {
                    transaction.Rollback();
                    throw new ProductNotFoundException(item.ProductId.ToString());
                }

                if (product.AvailableQuantity < item.Quantity)
                {
                    transaction.Rollback();
                    throw new InsufficientStockException(product.Name, product.AvailableQuantity);
                }

                var orderItemId = Guid.NewGuid();
                var orderItem = new OrderItem
                {
                    Id = orderItemId,
                    OrderId = order.Id,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    Price = product.DisplayPrice,
                    SelectedColor = item.SelectedColor,
                    SelectedSize = item.SelectedSize
                };

                await _connection.ExecuteAsync(@"
                    INSERT INTO order_items (id, order_id, product_id, quantity, price, selected_color, selected_size, created_at)
                    VALUES (@Id, @OrderId, @ProductId, @Quantity, @Price, @SelectedColor, @SelectedSize, @CreatedAt)",
                    new
                    {
                        orderItem.Id,
                        OrderId = orderItem.OrderId,
                        ProductId = orderItem.ProductId,
                        orderItem.Quantity,
                        orderItem.Price,
                        orderItem.SelectedColor,
                        orderItem.SelectedSize,
                        CreatedAt = DateTime.UtcNow
                    }, transaction);

                order.OrderItems.Add(orderItem);
                total += product.Price * item.Quantity;

                // Update stock directly via SQL (ProductService.UpdateProductAsync might not handle stock updates in transaction)
                await _connection.ExecuteAsync(
                    "UPDATE products SET available_quantity = available_quantity - @Quantity, updated_at = CURRENT_TIMESTAMP WHERE id = @ProductId",
                    new { Quantity = item.Quantity, ProductId = item.ProductId },
                    transaction);
            }

            order.TotalAmount = total;
            await _connection.ExecuteAsync(@"
                INSERT INTO orders (id, order_code, user_id, total_amount, status, shipping_address_id, created_at)
                VALUES (@Id, @OrderCode, @UserId, @TotalAmount, @Status, @ShippingAddressId, @CreatedAt)",
                new
                {
                    order.Id,
                    OrderCode = order.OrderCode,
                    order.UserId,
                    TotalAmount = order.TotalAmount,
                    Status = order.Status.ToString(),
                    order.ShippingAddressId,
                    order.CreatedAt
                },
                transaction);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        return order;
    }

    public async Task<Order?> UpdateOrderStatusAsync(
        Guid orderId,
        OrderStatus status,
        string? trackingNumber = null,
        string? trackingUrl = null)
    {
        var updateFields = new List<string>();
        var parameters = new DynamicParameters();
        parameters.Add("Id", orderId);
        parameters.Add("Status", status.ToString());
        parameters.Add("UpdatedAt", DateTime.UtcNow);

        updateFields.Add("status = @Status");
        updateFields.Add("updated_at = @UpdatedAt");

        if (!string.IsNullOrEmpty(trackingNumber))
        {
            updateFields.Add("tracking_number = @TrackingNumber");
            parameters.Add("TrackingNumber", trackingNumber);
        }

        if (!string.IsNullOrEmpty(trackingUrl))
        {
            updateFields.Add("tracking_url = @TrackingUrl");
            parameters.Add("TrackingUrl", trackingUrl);
        }

        var sql = $"UPDATE orders SET {string.Join(", ", updateFields)} WHERE id = @Id";
        await _connection.ExecuteAsync(sql, parameters);

        return await GetOrderByIdAsync(orderId);
    }

    public async Task<bool> OrderExistsAsync(Guid orderId)
    {
        var result = await _connection.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT id FROM orders WHERE id = @Id",
            new { Id = orderId });
        return result != null;
    }

    public async Task<Order?> GetOrderForTrackingAsync(string orderCode)
    {
        var order = await _connection.QueryFirstOrDefaultAsync<Order>(
            @"SELECT 
                id AS Id,
                order_code AS OrderCode,
                user_id AS UserId,
                guest_email AS GuestEmail,
                total_amount AS TotalAmount,
                status AS Status,
                coupon_id AS CouponId,
                shipping_address_id AS ShippingAddressId,
                stripe_payment_intent_id AS StripePaymentIntentId,
                tracking_number AS TrackingNumber,
                tracking_url AS TrackingUrl,
                notes AS Notes,
                cancellation_reason AS CancellationReason,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
              FROM orders WHERE order_code = @OrderCode",
            new { OrderCode = orderCode });

        return order;
    }

    public async Task<Order?> CancelOrderAsync(Guid orderId, Guid userId, string cancellationReason)
    {
        // Ensure connection is open
        if (_connection.State != ConnectionState.Open)
        {
            _connection.Open();
        }

        // Check if user is blocked from ordering
        var user = await _connection.QueryFirstOrDefaultAsync<dynamic>(
            @"SELECT 
                cancellation_count AS CancellationCount,
                order_blocked_until AS OrderBlockedUntil
              FROM users WHERE id = @UserId",
            new { UserId = userId });

        if (user != null && user.OrderBlockedUntil != null)
        {
            var blockedUntil = (DateTime)user.OrderBlockedUntil;
            if (blockedUntil > DateTime.UtcNow)
            {
                throw new OrderBlockedException(blockedUntil);
            }
        }

        // Get order and verify ownership
        var order = await GetOrderByIdAsync(orderId, userId);
        if (order == null)
        {
            return null;
        }

        // Check if order can be cancelled (only Pending or Paid orders can be cancelled)
        if (order.Status != OrderStatus.Pending && order.Status != OrderStatus.Paid)
        {
            throw new InvalidOrderStatusException(order.Status.ToString(), "Cancelled");
        }

        // Validate cancellation reason
        if (string.IsNullOrWhiteSpace(cancellationReason))
        {
            throw new ArgumentException("Cancellation reason is required");
        }

        if (cancellationReason.Length > 500)
        {
            throw new ArgumentException("Cancellation reason cannot exceed 500 characters");
        }

        using var transaction = _connection.BeginTransaction();
        try
        {
            // Update order status to Cancelled and save cancellation reason
            await _connection.ExecuteAsync(
                @"UPDATE orders 
                  SET status = @Status, 
                      cancellation_reason = @CancellationReason,
                      updated_at = CURRENT_TIMESTAMP 
                  WHERE id = @Id",
                new
                {
                    Status = OrderStatus.Cancelled.ToString(),
                    CancellationReason = cancellationReason,
                    Id = orderId
                },
                transaction);

            // Restore product stock
            foreach (var orderItem in order.OrderItems)
            {
                await _connection.ExecuteAsync(
                    "UPDATE products SET available_quantity = available_quantity + @Quantity, updated_at = CURRENT_TIMESTAMP WHERE id = @ProductId",
                    new { Quantity = orderItem.Quantity, ProductId = orderItem.ProductId },
                    transaction);
            }

            // Increment cancellation count and check if user should be blocked
            var currentCancellationCount = user?.CancellationCount ?? 0;
            var newCancellationCount = currentCancellationCount + 1;
            
            DateTime? blockUntil = null;
            if (newCancellationCount > 5)
            {
                // Block user for 1 week from now
                blockUntil = DateTime.UtcNow.AddDays(7);
            }

            await _connection.ExecuteAsync(
                @"UPDATE users 
                  SET cancellation_count = @CancellationCount,
                      order_blocked_until = @OrderBlockedUntil,
                      updated_at = CURRENT_TIMESTAMP
                  WHERE id = @UserId",
                new
                {
                    CancellationCount = newCancellationCount,
                    OrderBlockedUntil = blockUntil,
                    UserId = userId
                },
                transaction);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        // Reload order with cancellation reason
        order = await GetOrderByIdAsync(orderId, userId);
        return order;
    }

    private async Task LoadOrderItemsAsync(IReadOnlyList<Order> orders)
    {
        if (orders.Count == 0)
        {
            return;
        }

        var orderIds = orders.Select(o => o.Id).ToArray();
        var orderItems = (await _connection.QueryAsync<OrderItem>(
            @"SELECT 
                id AS Id,
                order_id AS OrderId,
                product_id AS ProductId,
                quantity AS Quantity,
                price AS Price,
                selected_color AS SelectedColor,
                selected_size AS SelectedSize,
                created_at AS CreatedAt
              FROM order_items WHERE order_id = ANY(@OrderIds)",
            new { OrderIds = orderIds })).ToList();

        if (orderItems.Count == 0)
        {
            foreach (var order in orders)
            {
                order.OrderItems = new List<OrderItem>();
            }
            return;
        }

        var productIds = orderItems.Select(i => i.ProductId).Distinct().ToArray();
        var products = (await _connection.QueryAsync<Product>(
            $"SELECT {PRODUCT_SELECT_COLUMNS} FROM products WHERE id = ANY(@ProductIds)",
            new { ProductIds = productIds })).ToList();

        var productLookup = products.ToDictionary(p => p.Id, p => p);
        foreach (var item in orderItems)
        {
            if (productLookup.TryGetValue(item.ProductId, out var product))
            {
                item.Product = product;
            }
            else
            {
                item.Product = new Product
                {
                    Id = item.ProductId,
                    Name = "Unknown",
                    Price = item.Price,
                    AvailableQuantity = 0
                };
            }
        }

        var itemsByOrder = orderItems.GroupBy(i => i.OrderId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var order in orders)
        {
            order.OrderItems = itemsByOrder.TryGetValue(order.Id, out var items)
                ? items
                : new List<OrderItem>();
        }
    }
}

