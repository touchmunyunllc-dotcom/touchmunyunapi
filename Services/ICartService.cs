using ECommerce.Models;

namespace ECommerce.Services;

public interface ICartService
{
    Task<CartItem> AddToCartAsync(Guid userId, Guid productId, int quantity, string? selectedColor = null, int? selectedSize = null);
    Task<bool> RemoveFromCartAsync(Guid userId, Guid itemId);
    Task<CartSummary> GetCartAsync(Guid userId, string? couponCode = null);
    Task<bool> UpdateCartItemQuantityAsync(Guid userId, Guid itemId, int quantity);
    Task<bool> ClearCartAsync(Guid userId);
    Task<CartSummary> ApplyCouponAsync(Guid userId, string couponCode);
    Task<decimal> CalculateTaxAsync(decimal subtotal);
}

