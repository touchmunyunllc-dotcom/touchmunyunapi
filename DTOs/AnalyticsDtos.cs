namespace ECommerce.DTOs;

public record DashboardStatsResponse
{
    public int TotalOrders { get; set; }
    public int CompletedOrders { get; set; }
    public int PendingOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AverageOrderValue { get; set; }
    public int TotalCustomers { get; set; }
    public object Period { get; set; } = new();
}

public record OrdersSummaryResponse
{
    public List<ECommerce.Services.StatusSummary> ByStatus { get; set; } = new();
    public object Period { get; set; } = new();
}

public record RevenueStatsResponse
{
    public List<ECommerce.Services.DailyRevenue> DailyRevenue { get; set; } = new();
    public object Period { get; set; } = new();
}

public record TopProductResponse
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int TotalQuantitySold { get; set; }
    public decimal TotalRevenue { get; set; }
}

public record TimeSeriesDataResponse
{
    public DateTime Date { get; set; }
    public int TotalOrders { get; set; }
    public int CompletedOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AverageOrderValue { get; set; }
    public int TotalCustomers { get; set; }
}
