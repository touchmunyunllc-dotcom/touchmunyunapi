namespace ECommerce.Services;

public interface ICouponService
{
    Task<decimal> ApplyCouponAsync(string code, decimal total);
    Task<bool> ValidateCouponAsync(string code);
}

