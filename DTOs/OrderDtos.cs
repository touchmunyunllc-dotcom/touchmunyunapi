namespace ECommerce.DTOs;

public record CreateOrderRequest(
    Guid? ShippingAddressId,
    List<OrderItemRequest> Items);

public record OrderItemRequest(Guid ProductId, int Quantity, string? SelectedColor = null, int? SelectedSize = null);

public record OrderTrackingResponse(
    string OrderCode,
    string Status,
    string? TrackingNumber,
    string? TrackingUrl,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record CancelOrderRequest(string Reason);
