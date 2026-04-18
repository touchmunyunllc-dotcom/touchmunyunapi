using ECommerce.DTOs;
using ECommerce.Models;
using ECommerce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly IEmailService _emailService;
    private readonly ISMSService _smsService;
    private readonly IAuditLogService _auditLog;

    public AdminController(
        IAdminService adminService,
        IEmailService emailService,
        ISMSService smsService,
        IAuditLogService auditLog)
    {
        _adminService = adminService;
        _emailService = emailService;
        _smsService = smsService;
        _auditLog = auditLog;
    }

    private Guid GetAdminUserId() =>
        Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : Guid.Empty;

    [HttpGet("orders")]
    [ProducesResponseType(typeof(AdminOrdersResponse), 200)]
    public async Task<IActionResult> GetAllOrders(
        [FromQuery] string? status = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var (orders, totalCount) = await _adminService.GetAllOrdersAsync(status, startDate, endDate, page, pageSize);
        return Ok(new AdminOrdersResponse
        {
            Orders = orders,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    [HttpGet("orders/{id:guid}")]
    [ProducesResponseType(typeof(AdminOrderDetailResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetOrderById(Guid id)
    {
        var order = await _adminService.GetOrderByIdAsync(id);
        if (order == null)
        {
            return NotFound();
        }

        return Ok(order);
    }

    [HttpPut("orders/{id:guid}/status")]
    [ProducesResponseType(typeof(Order), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateOrderStatus(Guid id, [FromBody] UpdateOrderStatusRequest request)
    {
        // Parse status to enum
        if (!Enum.TryParse<OrderStatus>(request.Status, out var orderStatus))
        {
            return BadRequest(new { message = "Invalid order status" });
        }

        try
        {
            // Get order details for notifications (before update)
            var existingOrder = await _adminService.GetOrderByIdAsync(id);
            if (existingOrder == null)
            {
                return NotFound();
            }

            // Update order status using service
            var updatedOrder = await _adminService.UpdateOrderStatusAsync(
                id,
                orderStatus,
                request.TrackingNumber,
                request.TrackingUrl,
                request.Notes);

            if (updatedOrder == null)
            {
                return NotFound();
            }

            // Send email and SMS notifications (handle both guest and customer)
            var email = existingOrder.UserEmail;
            var orderCode = existingOrder.OrderCode ?? existingOrder.Id.ToString();
            
            if (!string.IsNullOrEmpty(email))
            {
                await _emailService.SendOrderStatusUpdateAsync(
                    email,
                    orderCode,
                    request.Status,
                    request.Notes);

                await _smsService.SendOrderStatusUpdateAsync(
                    existingOrder.UserPhoneNumber ?? email,
                    orderCode,
                    request.Status);

                // If order is delivered, send invoice to customer
                if (orderStatus == OrderStatus.Delivered)
                {
                    var invoiceHtml = GenerateInvoiceHtml(existingOrder);
                    var invoiceUrl = $"{Request.Scheme}://{Request.Host}/api/admin/orders/{id}/invoice";
                    
                    await _emailService.SendInvoiceAsync(
                        email,
                        orderCode,
                        invoiceHtml,
                        invoiceUrl);
                }
            }

            _auditLog.LogAdminAction(GetAdminUserId(), "UpdateOrderStatus", "Order", id.ToString(),
                new { Status = request.Status, TrackingNumber = request.TrackingNumber });

            return Ok(updatedOrder);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("coupons")]
    [ProducesResponseType(typeof(Coupon), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CreateCoupon([FromBody] CreateCouponRequest request)
    {
        try
        {
            var coupon = await _adminService.CreateCouponAsync(
                request.Code,
                request.DiscountType,
                request.DiscountValue,
                request.ExpiryDate,
                request.UsageLimit,
                request.MinPurchaseAmount,
                request.MaxDiscountAmount);

            _auditLog.LogAdminAction(GetAdminUserId(), "CreateCoupon", "Coupon", coupon.Id.ToString(),
                new { request.Code, request.DiscountType, request.DiscountValue });

            return CreatedAtAction(nameof(GetCouponById), new { id = coupon.Id }, coupon);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("coupons/{id:guid}")]
    [ProducesResponseType(typeof(Coupon), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetCouponById(Guid id)
    {
        var coupon = await _adminService.GetCouponByIdAsync(id);
        if (coupon == null)
        {
            return NotFound();
        }

        return Ok(coupon);
    }

    [HttpPut("coupons/{id:guid}")]
    [ProducesResponseType(typeof(Coupon), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateCoupon(Guid id, [FromBody] UpdateCouponRequest request)
    {
        var updatedCoupon = await _adminService.UpdateCouponAsync(
            id,
            request.Code,
            request.DiscountValue,
            request.ExpiryDate,
            request.UsageLimit,
            request.IsActive,
            request.MinPurchaseAmount,
            request.MaxDiscountAmount);

        if (updatedCoupon == null)
        {
            return NotFound();
        }

        _auditLog.LogAdminAction(GetAdminUserId(), "UpdateCoupon", "Coupon", id.ToString(),
            new { request.Code, request.IsActive });

        return Ok(updatedCoupon);
    }

    [HttpDelete("coupons/{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteCoupon(Guid id)
    {
        var success = await _adminService.DeleteCouponAsync(id);
        if (!success)
        {
            return NotFound();
        }

        _auditLog.LogAdminAction(GetAdminUserId(), "DeleteCoupon", "Coupon", id.ToString());

        return NoContent();
    }

    [HttpPut("orders/{id:guid}/tracking")]
    [ProducesResponseType(typeof(Order), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateTracking(Guid id, [FromBody] UpdateTrackingRequest request)
    {
        // Get order details for notifications (before update)
        var existingOrder = await _adminService.GetOrderByIdAsync(id);
        if (existingOrder == null)
        {
            return NotFound();
        }

        var updatedOrder = await _adminService.UpdateOrderTrackingAsync(
            id,
            request.TrackingNumber,
            request.TrackingUrl);

        if (updatedOrder == null)
        {
            return NotFound();
        }

        // Send tracking notification
        var email = existingOrder.UserEmail;
        var orderCode = existingOrder.OrderCode ?? existingOrder.Id.ToString();
        
        if (!string.IsNullOrEmpty(email))
        {
            var trackingMessage = $"Your order {orderCode} tracking number: {request.TrackingNumber}";
            if (!string.IsNullOrEmpty(request.TrackingUrl))
            {
                trackingMessage += $"\nTrack here: {request.TrackingUrl}";
            }

            await _emailService.SendEmailAsync(
                email,
                $"Tracking Information - Order {orderCode}",
                trackingMessage,
                $@"
                    <h2>Your Order is Being Tracked</h2>
                    <p>Order Code: <strong>{orderCode}</strong></p>
                    <p>Tracking Number: <strong>{request.TrackingNumber}</strong></p>
                    {(string.IsNullOrEmpty(request.TrackingUrl) ? "" : $@"<p><a href=""{request.TrackingUrl}"">Track Your Package</a></p>")}
                ");

            await _smsService.SendSMSAsync(
                existingOrder.UserPhoneNumber ?? email,
                $"Order {orderCode} tracking: {request.TrackingNumber}");
        }

        _auditLog.LogAdminAction(GetAdminUserId(), "UpdateTracking", "Order", id.ToString(),
            new { request.TrackingNumber, request.TrackingUrl });

        return Ok(updatedOrder);
    }

    [HttpGet("orders/{id:guid}/invoice")]
    [ProducesResponseType(typeof(FileResult), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DownloadInvoice(Guid id)
    {
        var order = await _adminService.GetOrderByIdAsync(id);
        if (order == null)
        {
            return NotFound();
        }

        // Generate HTML invoice
        var invoiceHtml = GenerateInvoiceHtml(order);

        // Return HTML that can be printed/saved as PDF
        return Content(invoiceHtml, "text/html");
    }

    [HttpGet("customers")]
    [ProducesResponseType(typeof(CustomersResponse), 200)]
    public async Task<IActionResult> GetAllCustomers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null)
    {
        var (customers, totalCount) = await _adminService.GetAllCustomersAsync(page, pageSize, search);
        return Ok(new CustomersResponse
        {
            Customers = customers,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    [HttpGet("customers/{id:guid}")]
    [ProducesResponseType(typeof(CustomerDetailResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetCustomerById(Guid id)
    {
        var customer = await _adminService.GetCustomerByIdAsync(id);
        if (customer == null)
        {
            return NotFound();
        }

        return Ok(customer);
    }

    private string GenerateInvoiceHtml(AdminOrderDetailResponse order)
    {
        var orderCode = order.OrderCode ?? order.Id.ToString().Substring(0, 8).ToUpper();
        var customerName = order.UserName ?? "Guest";
        var customerEmail = order.UserEmail ?? order.GuestEmail ?? "N/A";
        var orderDate = order.CreatedAt.ToString("MMMM dd, yyyy");
        var orderTime = order.CreatedAt.ToString("hh:mm tt");

        var itemsHtml = string.Join("", order.OrderItems.Select(item => $@"
            <tr>
                <td style='padding: 10px; border-bottom: 1px solid #eee;'>{item.Product?.Name ?? "Unknown Product"}</td>
                <td style='padding: 10px; border-bottom: 1px solid #eee; text-align: center;'>{item.Quantity}</td>
                <td style='padding: 10px; border-bottom: 1px solid #eee; text-align: right;'>${item.Price:F2}</td>
                <td style='padding: 10px; border-bottom: 1px solid #eee; text-align: right;'>${item.Price * item.Quantity:F2}</td>
            </tr>"));

        var subtotal = order.OrderItems.Sum(item => item.Price * item.Quantity);
        var discount = subtotal - order.TotalAmount;
        var discountRow = discount > 0 ? $@"
            <tr>
                <td colspan='3' style='padding: 10px; text-align: right; font-weight: bold;'>Discount:</td>
                <td style='padding: 10px; text-align: right; font-weight: bold;'>-${discount:F2}</td>
            </tr>" : "";

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Invoice - {orderCode}</title>
    <style>
        body {{
            font-family: Arial, sans-serif;
            margin: 0;
            padding: 20px;
            color: #333;
        }}
        .invoice-container {{
            max-width: 800px;
            margin: 0 auto;
            background: white;
            padding: 30px;
            box-shadow: 0 0 10px rgba(0,0,0,0.1);
        }}
        .header {{
            display: flex;
            justify-content: space-between;
            margin-bottom: 30px;
            padding-bottom: 20px;
            border-bottom: 2px solid #333;
        }}
        .company-info h1 {{
            margin: 0;
            color: #333;
        }}
        .invoice-info {{
            text-align: right;
        }}
        .invoice-info h2 {{
            margin: 0;
            color: #666;
        }}
        .details {{
            display: flex;
            justify-content: space-between;
            margin-bottom: 30px;
        }}
        .customer-info, .order-info {{
            flex: 1;
        }}
        .customer-info h3, .order-info h3 {{
            margin-top: 0;
            color: #333;
        }}
        table {{
            width: 100%;
            border-collapse: collapse;
            margin-bottom: 20px;
        }}
        th {{
            background-color: #f5f5f5;
            padding: 12px;
            text-align: left;
            border-bottom: 2px solid #333;
        }}
        .total-row {{
            font-size: 18px;
            font-weight: bold;
            background-color: #f9f9f9;
        }}
        .footer {{
            margin-top: 40px;
            padding-top: 20px;
            border-top: 1px solid #eee;
            text-align: center;
            color: #666;
            font-size: 12px;
        }}
        @media print {{
            body {{ margin: 0; padding: 0; }}
            .invoice-container {{ box-shadow: none; }}
        }}
    </style>
</head>
<body>
    <div class='invoice-container'>
        <div class='header'>
            <div class='company-info'>
                <h1>TouchMunyun</h1>
                <p>E-Commerce Store</p>
            </div>
            <div class='invoice-info'>
                <h2>INVOICE</h2>
                <p><strong>Invoice #:</strong> {orderCode}</p>
                <p><strong>Date:</strong> {orderDate}</p>
                <p><strong>Time:</strong> {orderTime}</p>
            </div>
        </div>

        <div class='details'>
            <div class='customer-info'>
                <h3>Bill To:</h3>
                <p><strong>{customerName}</strong></p>
                <p>{customerEmail}</p>
            </div>
            <div class='order-info'>
                <h3>Order Details:</h3>
                <p><strong>Order Status:</strong> {order.Status}</p>
                <p><strong>Payment Method:</strong> {order.PaymentMethod ?? (order.Status == "Pending" ? "COD" : "Stripe")}</p>
            </div>
        </div>

        <table>
            <thead>
                <tr>
                    <th>Item</th>
                    <th style='text-align: center;'>Quantity</th>
                    <th style='text-align: right;'>Unit Price</th>
                    <th style='text-align: right;'>Total</th>
                </tr>
            </thead>
            <tbody>
                {itemsHtml}
                {discountRow}
                <tr class='total-row'>
                    <td colspan='3' style='padding: 15px; text-align: right;'>Total Amount:</td>
                    <td style='padding: 15px; text-align: right;'>${order.TotalAmount:F2}</td>
                </tr>
            </tbody>
        </table>

        <div class='footer'>
            <p>Thank you for your business!</p>
            <p>This is a computer-generated invoice and does not require a signature.</p>
        </div>
    </div>
</body>
</html>";
    }
}

