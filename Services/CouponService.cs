using ECommerce.Data;
using ECommerce.Utils;
using System.Data;
using Dapper;

namespace ECommerce.Services;

public class CouponService : ICouponService
{
    private readonly IDbConnection _connection;

    public CouponService(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<decimal> ApplyCouponAsync(string code, decimal total)
    {
        var coupon = await _connection.QueryFirstOrDefaultAsync<Models.Coupon>(
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
              FROM coupons WHERE code = @Code AND is_active = TRUE",
            new { Code = code.ToUpper() });

        if (coupon == null)
        {
            throw new CouponNotFoundException();
        }

        if (coupon.ExpiryDate.HasValue && coupon.ExpiryDate.Value < DateTime.UtcNow)
        {
            throw new CouponExpiredException();
        }

        if (coupon.UsageLimit.HasValue && coupon.UsageCount >= coupon.UsageLimit.Value)
        {
            throw new CouponUsageLimitException();
        }

        if (total < coupon.MinPurchaseAmount)
        {
            throw new MinPurchaseAmountException(coupon.MinPurchaseAmount);
        }

        decimal discount = 0;
        if (coupon.DiscountType == Models.DiscountType.Percentage)
        {
            discount = total * (coupon.DiscountValue / 100);
            
            // Apply max discount limit if set
            if (coupon.MaxDiscountAmount.HasValue && discount > coupon.MaxDiscountAmount.Value)
            {
                discount = coupon.MaxDiscountAmount.Value;
            }
        }
        else
        {
            discount = coupon.DiscountValue;
        }

        return Math.Max(0, total - discount);
    }

    public async Task<bool> ValidateCouponAsync(string code)
    {
        var coupon = await _connection.QueryFirstOrDefaultAsync<Models.Coupon>(
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
              FROM coupons WHERE code = @Code AND is_active = TRUE",
            new { Code = code.ToUpper() });

        if (coupon == null)
        {
            return false;
        }

        if (coupon.ExpiryDate.HasValue && coupon.ExpiryDate.Value < DateTime.UtcNow)
        {
            return false;
        }

        if (coupon.UsageLimit.HasValue && coupon.UsageCount >= coupon.UsageLimit.Value)
        {
            return false;
        }

        return true;
    }
}

