using System.Data;
using Dapper;

namespace ECommerce.Services;

public class AnalyticsService : IAnalyticsService
{
    private const string PaidStatusesSql = "'Paid', 'Packed', 'Shipped', 'Delivered'";

    private readonly IDbConnection _connection;

    public AnalyticsService(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<DashboardStats> GetDashboardStatsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var (start, end) = ResolveDateRange(startDate, endDate);

        var stats = await _connection.QueryFirstOrDefaultAsync(
            $@"SELECT 
                COUNT(DISTINCT o.id) as total_orders,
                COUNT(DISTINCT CASE WHEN o.status = 'Delivered' THEN o.id END) as completed_orders,
                COUNT(DISTINCT CASE WHEN o.status = 'Pending' OR o.status = 'Paid' THEN o.id END) as pending_orders,
                COALESCE(SUM(CASE WHEN o.status IN ({PaidStatusesSql}) THEN o.total_amount END), 0) as total_revenue,
                COALESCE(
                    SUM(CASE WHEN o.status IN ({PaidStatusesSql}) THEN o.total_amount END)
                    / NULLIF(COUNT(DISTINCT CASE WHEN o.status IN ({PaidStatusesSql}) THEN o.id END), 0),
                    0
                ) as average_order_value,
                COUNT(DISTINCT o.user_id) + COUNT(DISTINCT CASE WHEN o.guest_email IS NOT NULL THEN o.guest_email END) as total_customers
              FROM orders o
              WHERE o.status != 'Cancelled'
                AND o.created_at >= @StartDate AND o.created_at <= @EndDate",
            new { StartDate = start, EndDate = end });

        if (stats == null)
        {
            return new DashboardStats
            {
                StartDate = start,
                EndDate = end
            };
        }

        return new DashboardStats
        {
            TotalOrders = ToInt(stats.total_orders),
            CompletedOrders = ToInt(stats.completed_orders),
            PendingOrders = ToInt(stats.pending_orders),
            TotalRevenue = ToDecimal(stats.total_revenue),
            AverageOrderValue = ToDecimal(stats.average_order_value),
            TotalCustomers = ToInt(stats.total_customers),
            StartDate = start,
            EndDate = end
        };
    }

    public async Task<OrdersSummary> GetOrdersSummaryAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var (start, end) = ResolveDateRange(startDate, endDate);

        var summary = await _connection.QueryAsync(
            @"SELECT 
                status,
                COUNT(*) as count,
                SUM(total_amount) as total_amount
              FROM orders
              WHERE status != 'Cancelled'
                AND created_at >= @StartDate AND created_at <= @EndDate
              GROUP BY status",
            new { StartDate = start, EndDate = end });

        return new OrdersSummary
        {
            ByStatus = summary.Select(s => new StatusSummary
            {
                Status = s.status ?? string.Empty,
                Count = ToInt(s.count),
                TotalAmount = ToDecimal(s.total_amount)
            }).ToList(),
            StartDate = start,
            EndDate = end
        };
    }

    public async Task<RevenueStats> GetRevenueStatsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var (start, end) = ResolveDateRange(startDate, endDate);

        var dailyRevenue = await _connection.QueryAsync(
            $@"SELECT 
                created_at::date as date,
                SUM(total_amount) as revenue,
                COUNT(*) as order_count
              FROM orders
              WHERE status IN ({PaidStatusesSql})
                AND created_at >= @StartDate 
                AND created_at <= @EndDate
              GROUP BY created_at::date
              ORDER BY date",
            new { StartDate = start, EndDate = end });

        return new RevenueStats
        {
            DailyRevenue = dailyRevenue.Select(d => new DailyRevenue
            {
                Date = ToDateTime(d.date),
                Revenue = ToDecimal(d.revenue),
                OrderCount = ToInt(d.order_count)
            }).ToList(),
            StartDate = start,
            EndDate = end
        };
    }

    public async Task<List<TopProduct>> GetTopSellingProductsAsync(int limit = 10, DateTime? startDate = null, DateTime? endDate = null)
    {
        var (start, end) = ResolveDateRange(startDate, endDate);

        var topProducts = await _connection.QueryAsync(
            $@"SELECT 
                p.id,
                p.name,
                p.price,
                SUM(oi.quantity) as total_quantity,
                SUM(oi.quantity * oi.price) as total_revenue
              FROM order_items oi
              INNER JOIN products p ON oi.product_id = p.id
              INNER JOIN orders o ON oi.order_id = o.id
              WHERE o.status IN ({PaidStatusesSql})
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
            Price = ToDecimal(p.price),
            TotalQuantitySold = ToInt(p.total_quantity),
            TotalRevenue = ToDecimal(p.total_revenue)
        }).ToList();
    }

    public async Task<List<TimeSeriesData>> GetTimeSeriesDataAsync(DateTime? startDate = null, DateTime? endDate = null, string groupBy = "day")
    {
        var (start, end) = ResolveDateRange(startDate, endDate);

        var dateBucket = groupBy.ToLowerInvariant() switch
        {
            "month" => "DATE_TRUNC('month', o.created_at)",
            "year" => "DATE_TRUNC('year', o.created_at)",
            _ => "o.created_at::date"
        };

        var timeSeries = await _connection.QueryAsync(
            $@"SELECT 
                {dateBucket} as date,
                COUNT(DISTINCT o.id) as total_orders,
                COUNT(DISTINCT CASE WHEN o.status = 'Delivered' THEN o.id END) as completed_orders,
                COALESCE(SUM(CASE WHEN o.status IN ({PaidStatusesSql}) THEN o.total_amount END), 0) as total_revenue,
                COALESCE(
                    SUM(CASE WHEN o.status IN ({PaidStatusesSql}) THEN o.total_amount END)
                    / NULLIF(COUNT(DISTINCT CASE WHEN o.status IN ({PaidStatusesSql}) THEN o.id END), 0),
                    0
                ) as average_order_value,
                COUNT(DISTINCT o.user_id) + COUNT(DISTINCT CASE WHEN o.guest_email IS NOT NULL THEN o.guest_email END) as total_customers
              FROM orders o
              WHERE o.status != 'Cancelled'
                AND o.created_at >= @StartDate AND o.created_at <= @EndDate
              GROUP BY {dateBucket}
              ORDER BY date",
            new { StartDate = start, EndDate = end });

        return timeSeries.Select(t => new TimeSeriesData
        {
            Date = ToDateTime(t.date),
            TotalOrders = ToInt(t.total_orders),
            CompletedOrders = ToInt(t.completed_orders),
            TotalRevenue = ToDecimal(t.total_revenue),
            AverageOrderValue = ToDecimal(t.average_order_value),
            TotalCustomers = ToInt(t.total_customers)
        }).ToList();
    }

    private static (DateTime Start, DateTime End) ResolveDateRange(DateTime? startDate, DateTime? endDate)
    {
        var start = startDate ?? DateTime.UtcNow.AddYears(-1);
        var end = endDate ?? DateTime.UtcNow.AddDays(1);

        if (endDate.HasValue)
        {
            end = endDate.Value.Date.AddDays(1).AddTicks(-1);
        }

        return (start, end);
    }

    private static int ToInt(object? value) => value switch
    {
        null => 0,
        int i => i,
        long l => (int)l,
        short s => s,
        decimal d => (int)d,
        double dbl => (int)dbl,
        _ => Convert.ToInt32(value)
    };

    private static decimal ToDecimal(object? value) => value switch
    {
        null => 0m,
        decimal d => d,
        double dbl => (decimal)dbl,
        float f => (decimal)f,
        long l => l,
        int i => i,
        _ => Convert.ToDecimal(value)
    };

    private static DateTime ToDateTime(object? value) => value switch
    {
        null => DateTime.UtcNow,
        DateTime dt => dt,
        DateTimeOffset dto => dto.UtcDateTime,
        DateOnly dateOnly => dateOnly.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
        _ => DateTime.TryParse(value.ToString(), out var parsed) ? parsed : DateTime.UtcNow
    };
}
