using ECommerce.Models;

namespace ECommerce.DTOs;

public record CouponResponse
{
    public string Id { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public decimal DiscountAmount { get; init; }
    public string DiscountType { get; init; } = string.Empty;
}

public record CouponsResponse
{
    public List<Coupon> Coupons { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}
