using ECommerce.DTOs;
using ECommerce.Models;
using ECommerce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using FluentValidation;

namespace ECommerce.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CartController : ControllerBase
{
    private readonly ICartService _cartService;
    private readonly IValidator<AddToCartRequest> _addToCartValidator;
    private readonly IValidator<UpdateCartItemRequest> _updateCartItemValidator;
    private readonly IValidator<ApplyCouponRequest> _applyCouponValidator;

    public CartController(
        ICartService cartService,
        IValidator<AddToCartRequest> addToCartValidator,
        IValidator<UpdateCartItemRequest> updateCartItemValidator,
        IValidator<ApplyCouponRequest> applyCouponValidator)
    {
        _cartService = cartService;
        _addToCartValidator = addToCartValidator;
        _updateCartItemValidator = updateCartItemValidator;
        _applyCouponValidator = applyCouponValidator;
    }

    [HttpPost("add")]
    [ProducesResponseType(typeof(CartItemResponse), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> AddToCart([FromBody] AddToCartRequest request)
    {
        var validationResult = await _addToCartValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.Errors);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized();
        }

        try
        {
            var cartItem = await _cartService.AddToCartAsync(
                Guid.Parse(userId),
                request.ProductId,
                request.Quantity,
                request.SelectedColor,
                request.SelectedSize);

            return Ok(new CartItemResponse
            {
                Id = cartItem.Id,
                ProductId = cartItem.ProductId,
                Quantity = cartItem.Quantity,
                SelectedColor = cartItem.SelectedColor,
                SelectedSize = cartItem.SelectedSize
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("remove/{itemId:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RemoveFromCart(Guid itemId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized();
        }

        var removed = await _cartService.RemoveFromCartAsync(Guid.Parse(userId), itemId);
        return removed ? NoContent() : NotFound();
    }

    [HttpGet("view")]
    [ProducesResponseType(typeof(CartSummaryResponse), 200)]
    public async Task<IActionResult> ViewCart([FromQuery] string? couponCode = null)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized();
        }

        var cart = await _cartService.GetCartAsync(Guid.Parse(userId), couponCode);
        return Ok(MapToResponse(cart));
    }

    [HttpPut("item/{itemId:guid}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateCartItem(Guid itemId, [FromBody] UpdateCartItemRequest request)
    {
        var validationResult = await _updateCartItemValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.Errors);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized();
        }

        try
        {
            var updated = await _cartService.UpdateCartItemQuantityAsync(
                Guid.Parse(userId),
                itemId,
                request.Quantity);

            return updated ? Ok() : NotFound();
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("apply-coupon")]
    [ProducesResponseType(typeof(CartSummaryResponse), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> ApplyCoupon([FromBody] ApplyCouponRequest request)
    {
        var validationResult = await _applyCouponValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.Errors);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized();
        }

        try
        {
            var cart = await _cartService.ApplyCouponAsync(Guid.Parse(userId), request.CouponCode);
            return Ok(MapToResponse(cart));
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("clear")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> ClearCart()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized();
        }

        await _cartService.ClearCartAsync(Guid.Parse(userId));
        return NoContent();
    }

    private CartSummaryResponse MapToResponse(CartSummary cart)
    {
        return new CartSummaryResponse
        {
            Items = cart.Items.Select(item => new CartItemResponse
            {
                Id = item.Id,
                ProductId = item.ProductId,
                ProductName = item.Product?.Name ?? "",
                ProductPrice = item.Product?.DisplayPrice ?? 0,
                ProductImageUrl = item.Product?.Images?.FirstOrDefault() ?? "",
                Quantity = item.Quantity,
                Subtotal = (item.Product?.DisplayPrice ?? 0) * item.Quantity,
                SelectedColor = item.SelectedColor,
                SelectedSize = item.SelectedSize
            }).ToList(),
            Subtotal = cart.Subtotal,
            Tax = cart.Tax,
            Discount = cart.Discount,
            Total = cart.Total,
            AppliedCoupon = cart.AppliedCoupon != null ? new CouponInfo
            {
                Code = cart.AppliedCoupon.Code,
                DiscountValue = cart.AppliedCoupon.DiscountValue,
                DiscountType = cart.AppliedCoupon.DiscountType.ToString()
            } : null
        };
    }
}

