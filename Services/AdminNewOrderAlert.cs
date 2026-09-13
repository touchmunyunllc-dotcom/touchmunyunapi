namespace ECommerce.Services;

public record AdminNewOrderAlert(
    string OrderCode,
    decimal TotalAmount,
    int ItemCount,
    string Status,
    string? CustomerName,
    string? CustomerEmail,
    string? ShippingAddress,
    string PaymentMethod,
    bool IsGuest);
