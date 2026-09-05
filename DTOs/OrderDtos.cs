namespace ECommerce.DTOs;

public record CreateOrderRequest(
    Guid? ShippingAddressId,
    List<OrderItemRequest> Items);

public record OrderItemRequest(
    Guid ProductId,
    int Quantity,
    string? SelectedColor = null,
    string? SelectedSize = null,
    string? CustomNumber = null,
    string? WritingColor = null);

public record OrderTrackingResponse(
    string OrderCode,
    string Status,
    string? TrackingNumber,
    string? TrackingUrl,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record CancelOrderRequest(string Reason);

public record UserOrdersResponse
{
    public List<Models.Order> Orders { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}

public record UserOrdersSummaryResponse
{
    public int TotalCount { get; init; }
    public int Pending { get; init; }
    public int Delivered { get; init; }
    public int Cancelled { get; init; }
}
