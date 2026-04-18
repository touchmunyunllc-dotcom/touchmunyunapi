using ECommerce.DTOs;
using ECommerce.Models;
using ECommerce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ECommerce.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrderController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrderController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpGet("user")]
    [ProducesResponseType(typeof(List<Order>), 200)]
    public async Task<IActionResult> GetUserOrders(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int limit = 5)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null || !Guid.TryParse(userId, out var userIdGuid))
        {
            return Unauthorized();
        }

        // Validate limit (max 50 to prevent abuse)
        if (limit < 1 || limit > 50)
        {
            limit = 5;
        }

        var orders = await _orderService.GetUserOrdersAsync(userIdGuid, startDate, endDate, limit);
        return Ok(orders);
    }

    [HttpGet("by-payment-intent/{paymentIntentId}")]
    [ProducesResponseType(typeof(Order), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetOrderByPaymentIntent(string paymentIntentId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null || !Guid.TryParse(userId, out var userIdGuid))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(paymentIntentId))
        {
            return BadRequest();
        }

        var order = await _orderService.GetOrderByStripePaymentIntentAsync(userIdGuid, paymentIntentId.Trim());
        if (order == null)
        {
            return NotFound();
        }

        return Ok(order);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Order), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetOrderById(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null || !Guid.TryParse(userId, out var userIdGuid))
        {
            return Unauthorized();
        }

        var order = await _orderService.GetOrderByIdAsync(id, userIdGuid);
        if (order == null)
        {
            return NotFound();
        }

        return Ok(order);
    }

    /// <summary>Admin only: manual order creation (bypasses checkout tax/coupon/payment rules). Customers must use payment endpoints.</summary>
    [HttpPost("create")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(Order), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null || !Guid.TryParse(userId, out var userIdGuid))
        {
            return Unauthorized();
        }

        try
        {
            var orderItems = request.Items.Select(i => new ECommerce.Services.OrderItemRequest(i.ProductId, i.Quantity, i.SelectedColor, i.SelectedSize)).ToList();
            var order = await _orderService.CreateOrderAsync(
                userIdGuid,
                request.ShippingAddressId,
                orderItems);

            return CreatedAtAction(nameof(GetOrderById), new { id = order.Id }, order);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("track/{orderCode}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(OrderTrackingResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> TrackOrder(string orderCode)
    {
        var order = await _orderService.GetOrderForTrackingAsync(orderCode);
        if (order == null)
        {
            return NotFound(new { message = "Order not found" });
        }

        return Ok(new OrderTrackingResponse(
            order.OrderCode,
            order.Status.ToString(),
            order.TrackingNumber,
            order.TrackingUrl,
            order.CreatedAt,
            order.UpdatedAt));
    }

    [HttpGet("code/{orderCode}")]
    [ProducesResponseType(typeof(Order), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetOrderByCode(string orderCode)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null || !Guid.TryParse(userId, out var userIdGuid))
        {
            return Unauthorized();
        }

        var order = await _orderService.GetOrderByCodeAsync(orderCode, userIdGuid);
        if (order == null)
        {
            return NotFound();
        }

        return Ok(order);
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(Order), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> CancelOrder(Guid id, [FromBody] CancelOrderRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null || !Guid.TryParse(userId, out var userIdGuid))
        {
            return Unauthorized();
        }

        try
        {
            var order = await _orderService.CancelOrderAsync(id, userIdGuid, request.Reason);
            if (order == null)
            {
                return NotFound(new { message = "Order not found" });
            }

            return Ok(order);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception)
        {
            return BadRequest(new { message = "Failed to cancel order" });
        }
    }
}
