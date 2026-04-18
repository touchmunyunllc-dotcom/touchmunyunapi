using ECommerce.Models;

namespace ECommerce.Services;

public interface IOrderService
{
    Task<List<Order>> GetUserOrdersAsync(Guid userId, DateTime? startDate = null, DateTime? endDate = null, int limit = 5);
    Task<Order?> GetOrderByIdAsync(Guid orderId, Guid? userId = null);

    Task<Order?> GetOrderByStripePaymentIntentAsync(Guid userId, string paymentIntentId);
    Task<Order?> GetOrderByCodeAsync(string orderCode, Guid? userId = null);
    Task<Order> CreateOrderAsync(
        Guid userId,
        Guid? shippingAddressId,
        List<OrderItemRequest> items);
    Task<Order?> UpdateOrderStatusAsync(
        Guid orderId,
        OrderStatus status,
        string? trackingNumber = null,
        string? trackingUrl = null);
    Task<bool> OrderExistsAsync(Guid orderId);
    Task<Order?> GetOrderForTrackingAsync(string orderCode);
    Task<Order?> CancelOrderAsync(Guid orderId, Guid userId, string cancellationReason);
}

public record OrderItemRequest(Guid ProductId, int Quantity, string? SelectedColor = null, int? SelectedSize = null);

