using ECommerce.DTOs;
using ECommerce.Models;

namespace ECommerce.Services;

public interface IGuestService
{
    Task<GuestCheckoutPreviewResponse> PreviewGuestCheckoutAsync(
        List<GuestOrderItemRequest> items,
        string? couponCode,
        string? shippingCountry);

    Task<GuestOrderResult> CreateGuestOrderAsync(
        string email,
        string name,
        List<GuestOrderItem> items,
        decimal totalAmount,
        string currency,
        string? couponCode,
        GuestAddress shippingAddress,
        decimal? checkoutLatitude = null,
        decimal? checkoutLongitude = null);
    
    Task<GuestOrder?> GetGuestOrderAsync(string orderCode);
    
    Task<OrderTrackingInfo?> TrackGuestOrderAsync(string orderCode);

    /// <summary>Returns order code once guest Stripe checkout has been fulfilled (webhook).</summary>
    Task<string?> GetOrderCodeByPaymentIntentAsync(string paymentIntentId);
}

public record GuestOrderItem(
    Guid ProductId,
    string Name,
    decimal Price,
    int Quantity,
    string? SelectedColor = null,
    string? SelectedSize = null,
    string? CustomNumber = null,
    string? WritingColor = null);

public record GuestAddress(
    string AddressLine1,
    string? AddressLine2,
    string City,
    string State,
    string PostalCode,
    string Country,
    string Phone);

public record GuestOrderResult(
    string? OrderCode,
    string ClientSecret,
    string PaymentIntentId,
    Guid? OrderId);

public record GuestOrder(
    string OrderCode,
    string? GuestEmail,
    decimal TotalAmount,
    string Status,
    string? TrackingNumber,
    string? TrackingUrl,
    List<OrderItem> OrderItems);

public record OrderTrackingInfo(
    string OrderCode,
    string Status,
    string? TrackingNumber,
    string? TrackingUrl,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

