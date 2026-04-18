using ECommerce.Models;

namespace ECommerce.Services;

public interface IAdminService
{
    // Order Management
    Task<(List<AdminOrderResponse> Orders, int TotalCount)> GetAllOrdersAsync(
        string? status = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int page = 1,
        int pageSize = 10);
    
    Task<AdminOrderDetailResponse?> GetOrderByIdAsync(Guid orderId);
    
    Task<Order?> UpdateOrderStatusAsync(
        Guid orderId,
        OrderStatus status,
        string? trackingNumber = null,
        string? trackingUrl = null,
        string? notes = null);
    
    Task<Order?> UpdateOrderTrackingAsync(
        Guid orderId,
        string trackingNumber,
        string? trackingUrl);
    
    // Coupon Management
    Task<Coupon> CreateCouponAsync(
        string code,
        DiscountType discountType,
        decimal discountValue,
        DateTime? expiryDate = null,
        int? usageLimit = null,
        decimal minPurchaseAmount = 0,
        decimal? maxDiscountAmount = null);
    
    Task<Coupon?> GetCouponByIdAsync(Guid couponId);
    
    Task<Coupon?> UpdateCouponAsync(
        Guid couponId,
        string? code = null,
        decimal? discountValue = null,
        DateTime? expiryDate = null,
        int? usageLimit = null,
        bool? isActive = null,
        decimal? minPurchaseAmount = null,
        decimal? maxDiscountAmount = null);
    
    Task<bool> DeleteCouponAsync(Guid couponId);
    
    // Customer Management
    Task<(List<CustomerResponse> Customers, int TotalCount)> GetAllCustomersAsync(
        int page = 1,
        int pageSize = 10,
        string? search = null);
    
    Task<CustomerDetailResponse?> GetCustomerByIdAsync(Guid customerId);
}

public record CustomerResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public int TotalOrders { get; set; }
    public decimal TotalSpent { get; set; }
    public int CancellationCount { get; set; }
    public DateTime? OrderBlockedUntil { get; set; }
    public DateTime CreatedAt { get; set; }
}

public record CustomerDetailResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public int TotalOrders { get; set; }
    public decimal TotalSpent { get; set; }
    public int CancellationCount { get; set; }
    public DateTime? OrderBlockedUntil { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<CustomerOrderInfo> RecentOrders { get; set; } = new();
}

public record CustomerOrderInfo
{
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public record AdminOrderResponse
{
    public Guid Id { get; set; }
    public string? OrderCode { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string? UserEmail { get; set; }
    public string? GuestEmail { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CouponCode { get; set; }
    public string? TrackingNumber { get; set; }
    public string? TrackingUrl { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Notes { get; set; }
    public string? CancellationReason { get; set; }
    public string? ShippingAddress { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<AdminOrderItemInfo> OrderItems { get; set; } = new();
}

public record AdminOrderItemInfo
{
    public string ProductName { get; set; } = string.Empty;
    public string ProductSku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

public record AdminOrderDetailResponse
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string? UserEmail { get; set; }
    public string? UserPhoneNumber { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CouponCode { get; set; }
    public string? OrderCode { get; set; }
    public string? GuestEmail { get; set; }
    public string? PaymentMethod { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<OrderItem> OrderItems { get; set; } = new();
}

