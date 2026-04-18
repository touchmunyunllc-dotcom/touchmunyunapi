using ECommerce.Data;
using ECommerce.DTOs;
using ECommerce.Models;
using ECommerce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using Dapper;

namespace ECommerce.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CouponsController : ControllerBase
{
    private const string COUPONS_CACHE_KEY = "coupons:all";
    private const string COUPON_CACHE_KEY_PREFIX = "coupon:";
    private static readonly TimeSpan CACHE_EXPIRY = TimeSpan.FromMinutes(30); // Cache coupons for 30 minutes

    private readonly IDbConnection _connection;
    private readonly IRedisService _redisService;

    public CouponsController(IDbConnection connection, IRedisService redisService)
    {
        _connection = connection;
        _redisService = redisService;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(List<Coupon>), 200)]
    [ProducesResponseType(typeof(CouponsResponse), 200)]
    public async Task<IActionResult> GetAllCoupons(
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null)
    {
        // If pagination parameters are provided, return paginated response
        if (page.HasValue && pageSize.HasValue)
        {
            // Get total count
            var totalCount = await _connection.QueryFirstOrDefaultAsync<int>(
                "SELECT COUNT(*) FROM coupons");

            // Get paginated coupons
            var offset = (page.Value - 1) * pageSize.Value;
            var paginatedCoupons = await _connection.QueryAsync<Coupon>(
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
                  FROM coupons 
                  ORDER BY created_at DESC
                  LIMIT @PageSize OFFSET @Offset",
                new { PageSize = pageSize.Value, Offset = offset });

            return Ok(new CouponsResponse
            {
                Coupons = paginatedCoupons.ToList(),
                TotalCount = totalCount,
                Page = page.Value,
                PageSize = pageSize.Value,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize.Value)
            });
        }

        // Otherwise, return all coupons (backward compatibility)
        // Try to get from cache
        var cachedCoupons = await _redisService.GetAsync<List<Coupon>>(COUPONS_CACHE_KEY);
        if (cachedCoupons != null)
        {
            return Ok(cachedCoupons);
        }

        // Get from database with explicit column mapping
        var allCoupons = await _connection.QueryAsync<Coupon>(
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
              FROM coupons WHERE is_active = TRUE");

        var couponList = allCoupons.ToList();

        // Cache the coupons
        await _redisService.SetAsync(COUPONS_CACHE_KEY, couponList, CACHE_EXPIRY);

        return Ok(couponList);
    }

    /// <summary>Public list of active, non-expired coupons for marketing (e.g. homepage ticker). Does not expose admin-only data beyond normal coupon fields.</summary>
    [HttpGet("promo")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<Coupon>), 200)]
    public async Task<IActionResult> GetPromotionalCoupons()
    {
        var now = DateTime.UtcNow;
        var coupons = await _connection.QueryAsync<Coupon>(
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
              FROM coupons 
              WHERE is_active = TRUE 
                AND (expiry_date IS NULL OR expiry_date >= @Now)
                AND (usage_limit IS NULL OR usage_count < usage_limit)
              ORDER BY created_at DESC
              LIMIT 100",
            new { Now = now });

        return Ok(coupons.ToList());
    }

    [HttpGet("validate/{code}")]
    [ProducesResponseType(typeof(CouponResponse), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> ValidateCoupon(string code)
    {
        var cacheKey = $"{COUPON_CACHE_KEY_PREFIX}{code.ToUpper()}";
        
        // Try to get from cache
        var cachedCoupon = await _redisService.GetAsync<Coupon>(cacheKey);
        if (cachedCoupon != null)
        {
            // Still validate expiry and usage limit even if cached
            if (cachedCoupon.ExpiryDate.HasValue && cachedCoupon.ExpiryDate.Value < DateTime.UtcNow)
            {
                return BadRequest(new { message = "Coupon has expired" });
            }

            if (cachedCoupon.UsageLimit.HasValue && cachedCoupon.UsageCount >= cachedCoupon.UsageLimit.Value)
            {
                return BadRequest(new { message = "Coupon usage limit reached" });
            }

            return Ok(new CouponResponse
            {
                Id = cachedCoupon.Id.ToString(),
                Code = cachedCoupon.Code,
                DiscountAmount = cachedCoupon.DiscountValue,
                DiscountType = cachedCoupon.DiscountType.ToString()
            });
        }

        // Get from database with explicit column mapping
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
              FROM coupons WHERE code = @Code AND is_active = TRUE",
            new { Code = code.ToUpper() });

        if (coupon == null)
        {
            return NotFound(new { message = "Coupon not found or inactive" });
        }

        // Cache the coupon
        await _redisService.SetAsync(cacheKey, coupon, CACHE_EXPIRY);

        // Validate expiry and usage
        if (coupon.ExpiryDate.HasValue && coupon.ExpiryDate.Value < DateTime.UtcNow)
        {
            return BadRequest(new { message = "Coupon has expired" });
        }

        if (coupon.UsageLimit.HasValue && coupon.UsageCount >= coupon.UsageLimit.Value)
        {
            return BadRequest(new { message = "Coupon usage limit reached" });
        }

        return Ok(new CouponResponse
        {
            Id = coupon.Id.ToString(),
            Code = coupon.Code,
            DiscountAmount = coupon.DiscountValue,
            DiscountType = coupon.DiscountType.ToString()
        });
    }
}

