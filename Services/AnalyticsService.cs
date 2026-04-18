using System.Data;
using Dapper;

namespace ECommerce.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly IDbConnection _connection;

    public AnalyticsService(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<DashboardStats> GetDashboardStatsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        // If no dates provided, use a wider default range (last year) to catch all orders
        var start = startDate ?? DateTime.UtcNow.AddYears(-1);
        var end = endDate ?? DateTime.UtcNow.AddDays(1); // Include today
        
        // Ensure end date includes the full day
        if (endDate.HasValue)
        {
            end = end.Date.AddDays(1).AddTicks(-1);
        }

        var stats = await _connection.QueryFirstOrDefaultAsync(
            @"SELECT 
                COUNT(DISTINCT o.id) as total_orders,
                COUNT(DISTINCT CASE WHEN o.status = 'Delivered' THEN o.id END) as completed_orders,
                COUNT(DISTINCT CASE WHEN o.status = 'Pending' OR o.status = 'Paid' THEN o.id END) as pending_orders,
                COALESCE(SUM(CASE WHEN o.status = 'Delivered' THEN o.total_amount END), 0) as total_revenue,
                COALESCE(SUM(CASE WHEN o.status = 'Delivered' THEN o.total_amount END) / NULLIF(COUNT(DISTINCT CASE WHEN o.status = 'Delivered' THEN o.id END), 0), 0) as average_order_value,
                COUNT(DISTINCT o.user_id) + COUNT(DISTINCT CASE WHEN o.guest_email IS NOT NULL THEN o.guest_email END) as total_customers
              FROM orders o
              WHERE o.created_at >= @StartDate AND o.created_at <= @EndDate",
            new { StartDate = start, EndDate = end });

        return new DashboardStats
        {
            TotalOrders = (int)(stats.total_orders ?? 0),
            CompletedOrders = (int)(stats.completed_orders ?? 0),
            PendingOrders = (int)(stats.pending_orders ?? 0),
            TotalRevenue = stats.total_revenue ?? 0,
            AverageOrderValue = stats.average_order_value ?? 0,
            TotalCustomers = (int)(stats.total_customers ?? 0),
            StartDate = start,
            EndDate = end
        };
    }

    public async Task<OrdersSummary> GetOrdersSummaryAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var start = startDate ?? DateTime.UtcNow.AddYears(-1);
        var end = endDate ?? DateTime.UtcNow.AddDays(1);
        
        if (endDate.HasValue)
        {
            end = end.Date.AddDays(1).AddTicks(-1);
        }

        var summary = await _connection.QueryAsync(
            @"SELECT 
                status,
                COUNT(*) as count,
                SUM(total_amount) as total_amount
              FROM orders
              WHERE created_at >= @StartDate AND created_at <= @EndDate
              GROUP BY status",
            new { StartDate = start, EndDate = end });

        return new OrdersSummary
        {
            ByStatus = summary.Select(s => new StatusSummary
            {
                Status = s.status ?? string.Empty,
                Count = (int)(s.count ?? 0),
                TotalAmount = s.total_amount ?? 0
            }).ToList(),
            StartDate = start,
            EndDate = end
        };
    }

    public async Task<RevenueStats> GetRevenueStatsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var start = startDate ?? DateTime.UtcNow.AddYears(-1);
        var end = endDate ?? DateTime.UtcNow.AddDays(1);
        
        if (endDate.HasValue)
        {
            end = end.Date.AddDays(1).AddTicks(-1);
        }

        var dailyRevenue = await _connection.QueryAsync(
            @"SELECT 
                DATE(created_at) as date,
                SUM(total_amount) as revenue,
                COUNT(*) as order_count
              FROM orders
              WHERE status = 'Delivered' 
                AND created_at >= @StartDate 
                AND created_at <= @EndDate
              GROUP BY DATE(created_at)
              ORDER BY date",
            new { StartDate = start, EndDate = end });

        return new RevenueStats
        {
            DailyRevenue = dailyRevenue.Select(d => new DailyRevenue
            {
                Date = d.date,
                Revenue = d.revenue ?? 0,
                OrderCount = (int)(d.order_count ?? 0)
            }).ToList(),
            StartDate = start,
            EndDate = end
        };
    }

    public async Task<List<TopProduct>> GetTopSellingProductsAsync(int limit = 10, DateTime? startDate = null, DateTime? endDate = null)
    {
        var start = startDate ?? DateTime.UtcNow.AddYears(-1);
        var end = endDate ?? DateTime.UtcNow.AddDays(1);
        
        if (endDate.HasValue)
        {
            end = end.Date.AddDays(1).AddTicks(-1);
        }

        var topProducts = await _connection.QueryAsync(
            @"SELECT 
                p.id,
                p.name,
                p.price,
                SUM(oi.quantity) as total_quantity,
                SUM(oi.quantity * oi.price) as total_revenue
              FROM order_items oi
              INNER JOIN products p ON oi.product_id = p.id
              INNER JOIN orders o ON oi.order_id = o.id
              WHERE o.status = 'Delivered'
                AND o.created_at >= @StartDate
                AND o.created_at <= @EndDate
              GROUP BY p.id, p.name, p.price
              ORDER BY total_quantity DESC
              LIMIT @Limit",
            new { StartDate = start, EndDate = end, Limit = limit });

        return topProducts.Select(p => new TopProduct
        {
            ProductId = p.id,
            ProductName = p.name ?? string.Empty,
            Price = p.price ?? 0,
            TotalQuantitySold = (int)(p.total_quantity ?? 0),
            TotalRevenue = p.total_revenue ?? 0
        }).ToList();
    }

    public async Task<List<TimeSeriesData>> GetTimeSeriesDataAsync(DateTime? startDate = null, DateTime? endDate = null, string groupBy = "day")
    {
        var start = startDate ?? DateTime.UtcNow.AddYears(-1);
        var end = endDate ?? DateTime.UtcNow.AddDays(1);
        
        if (endDate.HasValue)
        {
            end = end.Date.AddDays(1).AddTicks(-1);
        }

        string dateFormat;
        switch (groupBy.ToLower())
        {
            case "month":
                dateFormat = "DATE_TRUNC('month', o.created_at)";
                break;
            case "year":
                dateFormat = "DATE_TRUNC('year', o.created_at)";
                break;
            case "day":
            default:
                dateFormat = "DATE(o.created_at)";
                break;
        }

        var timeSeries = await _connection.QueryAsync(
            $@"SELECT 
                {dateFormat} as date,
                COUNT(DISTINCT o.id) as total_orders,
                COUNT(DISTINCT CASE WHEN o.status = 'Delivered' THEN o.id END) as completed_orders,
                COALESCE(SUM(CASE WHEN o.status = 'Delivered' THEN o.total_amount END), 0) as total_revenue,
                COALESCE(SUM(CASE WHEN o.status = 'Delivered' THEN o.total_amount END) / NULLIF(COUNT(DISTINCT CASE WHEN o.status = 'Delivered' THEN o.id END), 0), 0) as average_order_value,
                COUNT(DISTINCT o.user_id) + COUNT(DISTINCT CASE WHEN o.guest_email IS NOT NULL THEN o.guest_email END) as total_customers
              FROM orders o
              WHERE o.created_at >= @StartDate AND o.created_at <= @EndDate
              GROUP BY {dateFormat}
              ORDER BY date",
            new { StartDate = start, EndDate = end });

        return timeSeries.Select(t => new TimeSeriesData
        {
            Date = t.date is DateTime dt ? dt : (t.date is DateTimeOffset dto ? dto.DateTime : DateTime.Parse(t.date?.ToString() ?? DateTime.UtcNow.ToString())),
            TotalOrders = (int)(t.total_orders ?? 0),
            CompletedOrders = (int)(t.completed_orders ?? 0),
            TotalRevenue = t.total_revenue ?? 0,
            AverageOrderValue = t.average_order_value ?? 0,
            TotalCustomers = (int)(t.total_customers ?? 0)
        }).ToList();
    }
}

