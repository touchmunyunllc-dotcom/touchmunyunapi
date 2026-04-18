namespace ECommerce.Services;

public interface IAnalyticsService
{
    Task<DashboardStats> GetDashboardStatsAsync(DateTime? startDate = null, DateTime? endDate = null);
    Task<OrdersSummary> GetOrdersSummaryAsync(DateTime? startDate = null, DateTime? endDate = null);
    Task<RevenueStats> GetRevenueStatsAsync(DateTime? startDate = null, DateTime? endDate = null);
    Task<List<TopProduct>> GetTopSellingProductsAsync(int limit = 10, DateTime? startDate = null, DateTime? endDate = null);
    Task<List<TimeSeriesData>> GetTimeSeriesDataAsync(DateTime? startDate = null, DateTime? endDate = null, string groupBy = "day");
}

public record DashboardStats
{
    public int TotalOrders { get; set; }
    public int CompletedOrders { get; set; }
    public int PendingOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AverageOrderValue { get; set; }
    public int TotalCustomers { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public record TimeSeriesData
{
    public DateTime Date { get; set; }
    public int TotalOrders { get; set; }
    public int CompletedOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AverageOrderValue { get; set; }
    public int TotalCustomers { get; set; }
}

public record OrdersSummary
{
    public List<StatusSummary> ByStatus { get; set; } = new();
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public record StatusSummary
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal TotalAmount { get; set; }
}

public record RevenueStats
{
    public List<DailyRevenue> DailyRevenue { get; set; } = new();
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public record DailyRevenue
{
    public DateTime Date { get; set; }
    public decimal Revenue { get; set; }
    public int OrderCount { get; set; }
}

public record TopProduct
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int TotalQuantitySold { get; set; }
    public decimal TotalRevenue { get; set; }
}

