using ECommerce.Models;
using ECommerce.Utils;
using System.Data;
using Dapper;

namespace ECommerce.Services;

public class AdminService : IAdminService
{
    private readonly IDbConnection _connection;
    private readonly IRedisService _redisService;
    private readonly IOrderService _orderService;
    private readonly ICouponService _couponService;
    private readonly INotificationQueueService _notificationQueue;
    private readonly ILogger<AdminService> _logger;

    public AdminService(
        IDbConnection connection,
        IRedisService redisService,
        IOrderService orderService,
        ICouponService couponService,
        INotificationQueueService notificationQueue,
        ILogger<AdminService> logger)
    {
        _connection = connection;
        _redisService = redisService;
        _orderService = orderService;
        _couponService = couponService;
        _notificationQueue = notificationQueue;
        _logger = logger;
    }

    public async Task<(List<AdminOrderResponse> Orders, int TotalCount)> GetAllOrdersAsync(
        string? status = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int page = 1,
        int pageSize = 10)
    {
        var sql = @"
            SELECT 
                o.id,
                o.order_code,
                o.user_id,
                o.guest_email,
                o.total_amount,
                o.status,
                o.tracking_number,
                o.tracking_url,
                o.stripe_payment_intent_id,
                o.notes,
                o.cancellation_reason,
                o.created_at,
                o.updated_at,
                u.name as user_name,
                u.email as user_email,
                c.code as coupon_code,
                a.address_line1,
                a.address_line2,
                a.city,
                a.state,
                a.postal_code,
                a.country
            FROM orders o
            LEFT JOIN users u ON o.user_id = u.id
            LEFT JOIN coupons c ON o.coupon_id = c.id
            LEFT JOIN addresses a ON o.shipping_address_id = a.id
            WHERE 1=1";

        var parameters = new DynamicParameters();

        if (!string.IsNullOrEmpty(status))
        {
            sql += " AND o.status = @Status";
            parameters.Add("Status", status);
        }

        if (startDate.HasValue)
        {
            sql += " AND o.created_at >= @StartDate";
            parameters.Add("StartDate", startDate.Value);
        }

        if (endDate.HasValue)
        {
            sql += " AND o.created_at <= @EndDate";
            parameters.Add("EndDate", endDate.Value);
        }

        // Get total count for pagination (before adding LIMIT/OFFSET)
        var countSql = @"SELECT COUNT(*) FROM orders o WHERE 1=1";
        var countParameters = new DynamicParameters();
        if (!string.IsNullOrEmpty(status))
        {
            countSql += " AND o.status = @Status";
            countParameters.Add("Status", status);
        }
        if (startDate.HasValue)
        {
            countSql += " AND o.created_at >= @StartDate";
            countParameters.Add("StartDate", startDate.Value);
        }
        if (endDate.HasValue)
        {
            countSql += " AND o.created_at <= @EndDate";
            countParameters.Add("EndDate", endDate.Value);
        }
        var totalCount = await _connection.QueryFirstOrDefaultAsync<int>(countSql, countParameters);

        // Add pagination and ordering
        var offset = (page - 1) * pageSize;
        sql += " ORDER BY o.created_at DESC LIMIT @PageSize OFFSET @Offset";
        parameters.Add("PageSize", pageSize);
        parameters.Add("Offset", offset);

        var orders = await _connection.QueryAsync(sql, parameters);
        var orderList = orders.ToList();
        
        // Load order items for each order
        var orderIds = orderList.Select(o => (Guid)o.id).ToList();
        var orderItems = new Dictionary<Guid, List<dynamic>>();
        
        if (orderIds.Any())
        {
            // Use IN clause instead of ANY for better compatibility
            var itemsSql = $@"
                SELECT 
                    oi.order_id,
                    oi.product_id,
                    oi.quantity,
                    oi.price,
                    p.name as product_name,
                    p.sku as product_sku
                FROM order_items oi
                LEFT JOIN products p ON oi.product_id = p.id
                WHERE oi.order_id IN ({string.Join(",", orderIds.Select((_, i) => $"@OrderId{i}"))})";
            
            var parametersDict = new Dictionary<string, object>();
            for (int i = 0; i < orderIds.Count; i++)
            {
                parametersDict[$"OrderId{i}"] = orderIds[i];
            }
            
            var items = await _connection.QueryAsync(itemsSql, parametersDict);
            foreach (var item in items)
            {
                var orderId = (Guid)item.order_id;
                if (!orderItems.ContainsKey(orderId))
                {
                    orderItems[orderId] = new List<dynamic>();
                }
                orderItems[orderId].Add(item);
            }
        }

        var ordersList = orderList.Select(o => new AdminOrderResponse
        {
            Id = o.id,
            OrderCode = o.order_code,
            UserId = o.user_id,
            UserName = o.user_name,
            UserEmail = o.user_email ?? o.guest_email,
            GuestEmail = o.guest_email,
            TotalAmount = o.total_amount,
            Status = o.status,
            CouponCode = o.coupon_code,
            TrackingNumber = o.tracking_number,
            TrackingUrl = o.tracking_url,
            PaymentMethod = !string.IsNullOrEmpty(o.stripe_payment_intent_id) ? "Stripe" : "Cash on Delivery",
            Notes = o.notes,
            CancellationReason = o.cancellation_reason,
            ShippingAddress = !string.IsNullOrEmpty(o.address_line1) 
                ? $"{o.address_line1}{(string.IsNullOrEmpty(o.address_line2) ? "" : ", " + o.address_line2)}, {o.city}, {o.state} {o.postal_code}, {o.country}"
                : null,
            CreatedAt = o.created_at,
            UpdatedAt = o.updated_at,
            OrderItems = orderItems.ContainsKey((Guid)o.id) 
                ? orderItems[(Guid)o.id].Select(item => new AdminOrderItemInfo
                {
                    ProductName = item.product_name ?? "Unknown Product",
                    ProductSku = item.product_sku ?? "N/A",
                    Quantity = item.quantity,
                    Price = item.price
                }).ToList()
                : new List<AdminOrderItemInfo>()
        }).ToList();

        return (ordersList, totalCount);
    }

    public async Task<AdminOrderDetailResponse?> GetOrderByIdAsync(Guid orderId)
    {
        var order = await _connection.QueryFirstOrDefaultAsync(
            @"
            SELECT o.*, u.name as user_name, u.email as user_email, u.phone_number as user_phone_number,
                   c.code as coupon_code, o.order_code, o.guest_email,
                   p.payment_method
            FROM orders o
            LEFT JOIN users u ON o.user_id = u.id
            LEFT JOIN coupons c ON o.coupon_id = c.id
            LEFT JOIN payments p ON p.order_id = o.id
            WHERE o.id = @Id",
            new { Id = orderId });

        if (order == null)
        {
            return null;
        }

        var orderItems = await _connection.QueryAsync(
            @"SELECT 
                oi.id,
                oi.order_id,
                oi.product_id,
                oi.quantity,
                oi.price,
                p.name as product_name
            FROM order_items oi
            INNER JOIN products p ON oi.product_id = p.id
            WHERE oi.order_id = @OrderId",
            new { OrderId = orderId });

        return new AdminOrderDetailResponse
        {
            Id = order.id,
            UserId = order.user_id,
            UserName = order.user_name,
            UserEmail = order.user_email ?? order.guest_email,
            UserPhoneNumber = (string?)order.user_phone_number,
            TotalAmount = order.total_amount,
            Status = order.status,
            CouponCode = order.coupon_code,
            OrderCode = order.order_code,
            GuestEmail = order.guest_email,
            PaymentMethod = (string?)order.payment_method,
            CreatedAt = order.created_at,
            OrderItems = orderItems.Select(oi => new OrderItem
            {
                Id = oi.id,
                OrderId = oi.order_id,
                ProductId = oi.product_id,
                Quantity = oi.quantity,
                Price = oi.price,
                Product = new Product { Name = oi.product_name ?? "Unknown Product" }
            }).ToList()
        };
    }

    public async Task<Order?> UpdateOrderStatusAsync(
        Guid orderId,
        OrderStatus status,
        string? trackingNumber = null,
        string? trackingUrl = null,
        string? notes = null)
    {
        // Validate status transition
        var existingOrder = await _connection.QueryFirstOrDefaultAsync<Order>(
            "SELECT * FROM orders WHERE id = @Id",
            new { Id = orderId });

        if (existingOrder == null)
        {
            return null;
        }

        // Allow admin to update to any status except going backwards from Delivered/Cancelled
        // Only prevent going backwards from final states
        if (existingOrder.Status == OrderStatus.Delivered && status != OrderStatus.Delivered)
        {
            throw new InvalidOrderStatusException("Delivered", status.ToString());
        }

        if (existingOrder.Status == OrderStatus.Cancelled && status != OrderStatus.Cancelled)
        {
            throw new InvalidOrderStatusException("Cancelled", status.ToString());
        }

        // Get user information for notifications (before update)
        var userInfo = await _connection.QueryFirstOrDefaultAsync<dynamic>(
            @"SELECT 
                u.email AS Email,
                u.name AS Name
              FROM orders o
              LEFT JOIN users u ON o.user_id = u.id
              WHERE o.id = @OrderId",
            new { OrderId = orderId });

        // Update order using OrderService
        var updatedOrder = await _orderService.UpdateOrderStatusAsync(
            orderId,
            status,
            trackingNumber,
            trackingUrl);

        // Queue notification in background (non-blocking)
        if (updatedOrder != null && userInfo != null)
        {
            try
            {
                // Get phone number if available (you may need to add this to users table or addresses)
                var phoneNumber = await _connection.QueryFirstOrDefaultAsync<string>(
                    @"SELECT phone FROM addresses WHERE user_id = (SELECT user_id FROM orders WHERE id = @OrderId) AND is_default = TRUE LIMIT 1",
                    new { OrderId = orderId });

                await _notificationQueue.EnqueueOrderStatusNotificationAsync(
                    new OrderStatusNotificationRequest(
                        updatedOrder.Id,
                        updatedOrder.OrderCode,
                        userInfo.Email ?? "",
                        phoneNumber,
                        status.ToString(),
                        trackingNumber,
                        trackingUrl,
                        notes));
            }
            catch (Exception ex)
            {
                // Log but don't fail the order update
                _logger.LogError(ex, "Failed to queue notification for order {OrderId}", orderId);
            }
        }

        // Update notes if provided
        if (!string.IsNullOrEmpty(notes) && updatedOrder != null)
        {
            await _connection.ExecuteAsync(
                "UPDATE orders SET notes = @Notes WHERE id = @Id",
                new { Notes = notes, Id = orderId });
        }

        return updatedOrder;
    }

    public async Task<Order?> UpdateOrderTrackingAsync(
        Guid orderId,
        string trackingNumber,
        string? trackingUrl)
    {
        var order = await _connection.QueryFirstOrDefaultAsync<Order>(
            "SELECT * FROM orders WHERE id = @Id",
            new { Id = orderId });

        if (order == null)
        {
            return null;
        }

        await _connection.ExecuteAsync(
            @"UPDATE orders 
              SET tracking_number = @TrackingNumber, 
                  tracking_url = @TrackingUrl,
                  updated_at = CURRENT_TIMESTAMP
              WHERE id = @Id",
            new
            {
                TrackingNumber = trackingNumber,
                TrackingUrl = trackingUrl,
                Id = orderId
            });

        return await _connection.QueryFirstOrDefaultAsync<Order>(
            "SELECT * FROM orders WHERE id = @Id",
            new { Id = orderId });
    }

    public async Task<Coupon> CreateCouponAsync(
        string code,
        DiscountType discountType,
        decimal discountValue,
        DateTime? expiryDate = null,
        int? usageLimit = null,
        decimal minPurchaseAmount = 0,
        decimal? maxDiscountAmount = null)
    {
        var existingCoupon = await _connection.QueryFirstOrDefaultAsync<Coupon>(
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
            new { Code = code.ToUpper() });

        if (existingCoupon != null)
        {
            throw new DuplicateCouponException(code.ToUpper());
        }

        var coupon = new Coupon
        {
            Id = Guid.NewGuid(),
            Code = code.ToUpper(),
            DiscountType = discountType,
            DiscountValue = discountValue,
            ExpiryDate = expiryDate,
            UsageLimit = usageLimit,
            IsActive = true,
            MinPurchaseAmount = minPurchaseAmount,
            MaxDiscountAmount = maxDiscountAmount,
            CreatedAt = DateTime.UtcNow
        };

        await _connection.ExecuteAsync(@"
            INSERT INTO coupons (id, code, discount_type, discount_value, expiry_date, usage_limit, 
                               usage_count, is_active, min_purchase_amount, max_discount_amount, created_at)
            VALUES (@Id, @Code, @DiscountType, @DiscountValue, @ExpiryDate, @UsageLimit, @UsageCount,
                   @IsActive, @MinPurchaseAmount, @MaxDiscountAmount, @CreatedAt)",
            new
            {
                coupon.Id,
                Code = coupon.Code,
                DiscountType = coupon.DiscountType.ToString(),
                DiscountValue = coupon.DiscountValue,
                ExpiryDate = coupon.ExpiryDate,
                coupon.UsageLimit,
                UsageCount = coupon.UsageCount,
                IsActive = coupon.IsActive,
                coupon.MinPurchaseAmount,
                coupon.MaxDiscountAmount,
                CreatedAt = coupon.CreatedAt
            });

        // Invalidate coupon cache
        await _redisService.DeleteAsync("coupons:all");
        await _redisService.DeleteByPatternAsync("coupon:*");

        return coupon;
    }

    public async Task<Coupon?> GetCouponByIdAsync(Guid couponId)
    {
        return await _connection.QueryFirstOrDefaultAsync<Coupon>(
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
              FROM coupons WHERE id = @Id",
            new { Id = couponId });
    }

    public async Task<Coupon?> UpdateCouponAsync(
        Guid couponId,
        string? code = null,
        decimal? discountValue = null,
        DateTime? expiryDate = null,
        int? usageLimit = null,
        bool? isActive = null,
        decimal? minPurchaseAmount = null,
        decimal? maxDiscountAmount = null)
    {
        var coupon = await _connection.QueryFirstOrDefaultAsync<Coupon>(
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
              FROM coupons WHERE id = @Id",
            new { Id = couponId });

        if (coupon == null)
        {
            return null;
        }

        var updateFields = new List<string>();
        var parameters = new DynamicParameters();
        parameters.Add("Id", couponId);

        if (code != null)
        {
            updateFields.Add("code = @Code");
            parameters.Add("Code", code.ToUpper());
        }

        if (discountValue.HasValue)
        {
            updateFields.Add("discount_value = @DiscountValue");
            parameters.Add("DiscountValue", discountValue.Value);
        }

        if (expiryDate.HasValue)
        {
            updateFields.Add("expiry_date = @ExpiryDate");
            parameters.Add("ExpiryDate", expiryDate.Value);
        }

        if (usageLimit.HasValue)
        {
            updateFields.Add("usage_limit = @UsageLimit");
            parameters.Add("UsageLimit", usageLimit.Value);
        }

        if (isActive.HasValue)
        {
            updateFields.Add("is_active = @IsActive");
            parameters.Add("IsActive", isActive.Value);
        }

        if (minPurchaseAmount.HasValue)
        {
            updateFields.Add("min_purchase_amount = @MinPurchaseAmount");
            parameters.Add("MinPurchaseAmount", minPurchaseAmount.Value);
        }

        if (maxDiscountAmount.HasValue)
        {
            updateFields.Add("max_discount_amount = @MaxDiscountAmount");
            parameters.Add("MaxDiscountAmount", maxDiscountAmount.Value);
        }

        if (updateFields.Count == 0)
        {
            return coupon;
        }

        var sql = $"UPDATE coupons SET {string.Join(", ", updateFields)} WHERE id = @Id";
        await _connection.ExecuteAsync(sql, parameters);

        var updatedCoupon = await _connection.QueryFirstOrDefaultAsync<Coupon>(
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
              FROM coupons WHERE id = @Id",
            new { Id = couponId });

        // Invalidate coupon cache
        await _redisService.DeleteAsync("coupons:all");
        await _redisService.DeleteByPatternAsync("coupon:*");

        return updatedCoupon;
    }

    public async Task<bool> DeleteCouponAsync(Guid couponId)
    {
        var coupon = await _connection.QueryFirstOrDefaultAsync<Coupon>(
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
              FROM coupons WHERE id = @Id",
            new { Id = couponId });

        if (coupon == null)
        {
            return false;
        }

        // Soft delete by setting is_active to false
        await _connection.ExecuteAsync(
            "UPDATE coupons SET is_active = FALSE WHERE id = @Id",
            new { Id = couponId });

        // Invalidate coupon cache
        await _redisService.DeleteAsync("coupons:all");
        await _redisService.DeleteByPatternAsync("coupon:*");

        return true;
    }

    public async Task<(List<CustomerResponse> Customers, int TotalCount)> GetAllCustomersAsync(
        int page = 1,
        int pageSize = 10,
        string? search = null)
    {
        var sql = @"
            SELECT 
                u.id,
                u.name,
                u.email,
                u.role,
                u.provider,
                u.cancellation_count,
                u.order_blocked_until,
                u.created_at,
                COUNT(DISTINCT o.id) as total_orders,
                COALESCE(SUM(CASE WHEN o.status = 'Delivered' THEN o.total_amount ELSE 0 END), 0) as total_spent
            FROM users u
            LEFT JOIN orders o ON u.id = o.user_id
            WHERE 1=1";

        var parameters = new DynamicParameters();

        if (!string.IsNullOrEmpty(search))
        {
            sql += " AND (u.name ILIKE @Search OR u.email ILIKE @Search)";
            parameters.Add("Search", $"%{search}%");
        }

        sql += " GROUP BY u.id, u.name, u.email, u.role, u.provider, u.cancellation_count, u.order_blocked_until, u.created_at";

        // Get total count - use a separate query to count distinct users
        var countSql = @"
            SELECT COUNT(DISTINCT u.id)
            FROM users u
            WHERE 1=1";
        
        if (!string.IsNullOrEmpty(search))
        {
            countSql += " AND (u.name ILIKE @Search OR u.email ILIKE @Search)";
        }
        
        var totalCountLong = await _connection.QueryFirstOrDefaultAsync<long>(countSql, parameters);
        var totalCount = (int)totalCountLong;

        // Add pagination
        sql += " ORDER BY u.created_at DESC LIMIT @PageSize OFFSET @Offset";
        parameters.Add("PageSize", pageSize);
        parameters.Add("Offset", (page - 1) * pageSize);

        var customers = await _connection.QueryAsync(sql, parameters);

        var customerList = customers.Select(c => new CustomerResponse
        {
            Id = c.id,
            Name = c.name,
            Email = c.email,
            Role = c.role,
            Provider = c.provider,
            TotalOrders = (int)(c.total_orders ?? 0),
            TotalSpent = c.total_spent ?? 0,
            CancellationCount = c.cancellation_count ?? 0,
            OrderBlockedUntil = c.order_blocked_until,
            CreatedAt = c.created_at
        }).ToList();

        return (customerList, totalCount);
    }

    public async Task<CustomerDetailResponse?> GetCustomerByIdAsync(Guid customerId)
    {
        var customer = await _connection.QueryFirstOrDefaultAsync(
            @"SELECT 
                u.id,
                u.name,
                u.email,
                u.role,
                u.provider,
                u.cancellation_count,
                u.order_blocked_until,
                u.created_at,
                u.updated_at,
                COUNT(DISTINCT o.id) as total_orders,
                COALESCE(SUM(CASE WHEN o.status = 'Delivered' THEN o.total_amount ELSE 0 END), 0) as total_spent
            FROM users u
            LEFT JOIN orders o ON u.id = o.user_id
            WHERE u.id = @CustomerId
            GROUP BY u.id, u.name, u.email, u.role, u.provider, u.cancellation_count, u.order_blocked_until, u.created_at, u.updated_at",
            new { CustomerId = customerId });

        if (customer == null)
        {
            return null;
        }

        var recentOrders = await _connection.QueryAsync(
            @"SELECT 
                o.id as order_id,
                o.order_code,
                o.total_amount,
                o.status,
                o.created_at
            FROM orders o
            WHERE o.user_id = @CustomerId
            ORDER BY o.created_at DESC
            LIMIT 10",
            new { CustomerId = customerId });

        return new CustomerDetailResponse
        {
            Id = customer.id,
            Name = customer.name,
            Email = customer.email,
            Role = customer.role,
            Provider = customer.provider,
            TotalOrders = (int)(customer.total_orders ?? 0),
            TotalSpent = customer.total_spent ?? 0,
            CancellationCount = customer.cancellation_count ?? 0,
            OrderBlockedUntil = customer.order_blocked_until,
            CreatedAt = customer.created_at,
            UpdatedAt = customer.updated_at,
            RecentOrders = recentOrders.Select(o => new CustomerOrderInfo
            {
                OrderId = o.order_id,
                OrderCode = o.order_code ?? o.order_id.ToString(),
                TotalAmount = o.total_amount,
                Status = o.status,
                CreatedAt = o.created_at
            }).ToList()
        };
    }
}

