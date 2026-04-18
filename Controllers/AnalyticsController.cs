using ECommerce.DTOs;
using ECommerce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Controllers;

[ApiController]
[Route("api/admin/[controller]")]
[Authorize(Roles = "Admin")]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;

    public AnalyticsController(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(DashboardStatsResponse), 200)]
    public async Task<IActionResult> GetDashboardStats(
        [FromQuery] string? startDate = null,
        [FromQuery] string? endDate = null)
    {
        DateTime? parsedStartDate = null;
        DateTime? parsedEndDate = null;
        
        if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out var start))
        {
            parsedStartDate = start.ToUniversalTime();
        }
        
        if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out var end))
        {
            // Set end date to end of day in UTC
            parsedEndDate = end.Date.AddDays(1).AddTicks(-1).ToUniversalTime();
        }
        
        var stats = await _analyticsService.GetDashboardStatsAsync(parsedStartDate, parsedEndDate);

        return Ok(new DashboardStatsResponse
        {
            TotalOrders = stats.TotalOrders,
            CompletedOrders = stats.CompletedOrders,
            PendingOrders = stats.PendingOrders,
            TotalRevenue = stats.TotalRevenue,
            AverageOrderValue = stats.AverageOrderValue,
            TotalCustomers = stats.TotalCustomers,
            Period = new { StartDate = stats.StartDate, EndDate = stats.EndDate }
        });
    }

    [HttpGet("orders/summary")]
    [ProducesResponseType(typeof(OrdersSummaryResponse), 200)]
    public async Task<IActionResult> GetOrdersSummary(
        [FromQuery] string? startDate = null,
        [FromQuery] string? endDate = null)
    {
        DateTime? parsedStartDate = null;
        DateTime? parsedEndDate = null;
        
        if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out var start))
        {
            parsedStartDate = start.ToUniversalTime();
        }
        
        if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out var end))
        {
            parsedEndDate = end.Date.AddDays(1).AddTicks(-1).ToUniversalTime();
        }
        
        var summary = await _analyticsService.GetOrdersSummaryAsync(parsedStartDate, parsedEndDate);

        return Ok(new OrdersSummaryResponse
        {
            ByStatus = summary.ByStatus.Select(s => new StatusSummary
            {
                Status = s.Status,
                Count = s.Count,
                TotalAmount = s.TotalAmount
            }).ToList(),
            Period = new { StartDate = summary.StartDate, EndDate = summary.EndDate }
        });
    }

    [HttpGet("revenue")]
    [ProducesResponseType(typeof(RevenueStatsResponse), 200)]
    public async Task<IActionResult> GetRevenueStats(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var revenue = await _analyticsService.GetRevenueStatsAsync(startDate, endDate);

        return Ok(new RevenueStatsResponse
        {
            DailyRevenue = revenue.DailyRevenue.Select(d => new DailyRevenue
            {
                Date = d.Date,
                Revenue = d.Revenue,
                OrderCount = d.OrderCount
            }).ToList(),
            Period = new { StartDate = revenue.StartDate, EndDate = revenue.EndDate }
        });
    }

    [HttpGet("products/top-selling")]
    [ProducesResponseType(typeof(List<TopProductResponse>), 200)]
    public async Task<IActionResult> GetTopSellingProducts(
        [FromQuery] int limit = 10,
        [FromQuery] string? startDate = null,
        [FromQuery] string? endDate = null)
    {
        DateTime? parsedStartDate = null;
        DateTime? parsedEndDate = null;
        
        if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out var start))
        {
            parsedStartDate = start.ToUniversalTime();
        }
        
        if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out var end))
        {
            parsedEndDate = end.Date.AddDays(1).AddTicks(-1).ToUniversalTime();
        }
        
        var topProducts = await _analyticsService.GetTopSellingProductsAsync(limit, parsedStartDate, parsedEndDate);

        return Ok(topProducts.Select(p => new TopProductResponse
        {
            ProductId = p.ProductId,
            ProductName = p.ProductName,
            Price = p.Price,
            TotalQuantitySold = p.TotalQuantitySold,
            TotalRevenue = p.TotalRevenue
        }).ToList());
    }

    [HttpGet("time-series")]
    [ProducesResponseType(typeof(List<TimeSeriesDataResponse>), 200)]
    public async Task<IActionResult> GetTimeSeriesData(
        [FromQuery] string? startDate = null,
        [FromQuery] string? endDate = null,
        [FromQuery] string groupBy = "day")
    {
        DateTime? parsedStartDate = null;
        DateTime? parsedEndDate = null;
        
        if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out var start))
        {
            parsedStartDate = start.ToUniversalTime();
        }
        
        if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out var end))
        {
            parsedEndDate = end.Date.AddDays(1).AddTicks(-1).ToUniversalTime();
        }
        
        var timeSeries = await _analyticsService.GetTimeSeriesDataAsync(parsedStartDate, parsedEndDate, groupBy);
        return Ok(timeSeries.Select(t => new TimeSeriesDataResponse
        {
            Date = t.Date,
            TotalOrders = t.TotalOrders,
            CompletedOrders = t.CompletedOrders,
            TotalRevenue = t.TotalRevenue,
            AverageOrderValue = t.AverageOrderValue,
            TotalCustomers = t.TotalCustomers
        }).ToList());
    }
}
