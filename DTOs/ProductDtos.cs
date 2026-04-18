using ECommerce.Models;

namespace ECommerce.DTOs;

public record CreateProductRequest(
    string Name,
    string Description,
    decimal Price,
    decimal? SalePrice,
    string ImageUrl,
    string Category,
    int Stock,
    List<string>? Colors = null,
    List<int>? Sizes = null);

public record UpdateProductRequest(
    string? Name,
    string? Description,
    decimal? Price,
    decimal? SalePrice,
    string? ImageUrl,
    string? Category,
    int? Stock,
    List<string>? Colors = null,
    List<int>? Sizes = null);

public record BulkPriceUpdateRequest(
    List<Guid> ProductIds,
    decimal? Price = null,
    decimal? SalePrice = null,
    string? AdjustmentType = null, // "percentage" or "fixed"
    decimal? AdjustmentValue = null);

public record UpdatePriceRequest(
    decimal? Price = null,
    decimal? SalePrice = null,
    bool ClearSalePrice = false);

public record ProductsResponse
{
    public List<Product> Products { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}
