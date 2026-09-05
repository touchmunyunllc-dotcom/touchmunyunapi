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

    [HttpGet("user/summary")]
    [ProducesResponseType(typeof(UserOrdersSummaryResponse), 200)]
    public async Task<IActionResult> GetUserOrdersSummary(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null || !Guid.TryParse(userId, out var userIdGuid))
        {
            return Unauthorized();
        }

        var summary = await _orderService.GetUserOrdersSummaryAsync(userIdGuid, startDate, endDate);

        return Ok(new UserOrdersSummaryResponse
        {
            TotalCount = summary.TotalCount,
            Pending = summary.Pending,
            Delivered = summary.Delivered,
            Cancelled = summary.Cancelled,
        });
    }

    [HttpGet("user")]
    [ProducesResponseType(typeof(List<Order>), 200)]
    [ProducesResponseType(typeof(UserOrdersResponse), 200)]
    public async Task<IActionResult> GetUserOrders(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? statusGroup = null,
        [FromQuery] int limit = 5)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null || !Guid.TryParse(userId, out var userIdGuid))
        {
            return Unauthorized();
        }

        if (page.HasValue && pageSize.HasValue)
        {
            var safePage = page.Value < 1 ? 1 : page.Value;
            var safePageSize = pageSize.Value switch
            {
                < 1 => 10,
                > 50 => 50,
                _ => pageSize.Value
            };

            var (orders, totalCount) = await _orderService.GetUserOrdersPaginatedAsync(
                userIdGuid, startDate, endDate, safePage, safePageSize, statusGroup);

            return Ok(new UserOrdersResponse
            {
                Orders = orders,
                TotalCount = totalCount,
                Page = safePage,
                PageSize = safePageSize,
                TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)safePageSize)
            });
        }

        // Backward compatibility for callers using limit only
        if (limit < 1 || limit > 50)
        {
            limit = 5;
        }

        var legacyOrders = await _orderService.GetUserOrdersAsync(userIdGuid, startDate, endDate, limit);
        return Ok(legacyOrders);
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
            var orderItems = request.Items.Select(i => new ECommerce.Services.OrderItemRequest(
                i.ProductId, i.Quantity, i.SelectedColor, i.SelectedSize, i.CustomNumber, i.WritingColor)).ToList();
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
