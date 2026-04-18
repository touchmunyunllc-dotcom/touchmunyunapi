using ECommerce.Models;
using ECommerce.Services;

namespace ECommerce.DTOs;

public record UpdateOrderStatusRequest(
    string Status, 
    string? Notes,
    string? TrackingNumber = null,
    string? TrackingUrl = null);
public record UpdateTrackingRequest(string TrackingNumber, string? TrackingUrl);
public record AdminOrdersResponse
{
    public List<AdminOrderResponse> Orders { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}
public record CreateCouponRequest(
    string Code,
    DiscountType DiscountType,
    decimal DiscountValue,
    DateTime? ExpiryDate,
    int? UsageLimit,
    decimal MinPurchaseAmount,
    decimal? MaxDiscountAmount);
public record UpdateCouponRequest(
    string? Code,
    decimal? DiscountValue,
    DateTime? ExpiryDate,
    int? UsageLimit,
    bool? IsActive,
    decimal? MinPurchaseAmount = null,
    decimal? MaxDiscountAmount = null);

public record CustomersResponse
{
    public List<CustomerResponse> Customers { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}
