using ECommerce.Models;

namespace ECommerce.Services;

public interface IProductService
{
    Task<List<Product>> GetAllProductsAsync(
        string? category = null,
        string? search = null,
        decimal? minPrice = null,
        decimal? maxPrice = null);
    
    Task<(List<Product> Products, int TotalCount)> GetAllProductsPaginatedAsync(
        string? category = null,
        string? search = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        int page = 1,
        int pageSize = 10);
    
    Task<List<Product>> GetNewArrivalsAsync(int limit = 50);
    
    Task<List<Product>> GetBestSellersAsync(int limit = 50);
    
    Task<Product?> GetProductByIdAsync(Guid id);
    
    Task<Product> CreateProductAsync(
        string name,
        string description,
        decimal price,
        List<string> images,
        string category,
        int availableQuantity,
        string? sku = null,
        List<string>? colors = null,
        List<int>? sizes = null);
    
    Task<Product?> UpdateProductAsync(
        Guid id,
        string? name = null,
        string? description = null,
        decimal? price = null,
        List<string>? images = null,
        string? category = null,
        int? availableQuantity = null,
        string? sku = null,
        bool? isActive = null,
        List<string>? colors = null,
        List<int>? sizes = null);
    
    Task<bool> DeleteProductAsync(Guid id);
    
    Task<bool> ProductExistsAsync(Guid id);
    
    Task InvalidateProductCacheAsync(Guid? productId = null);
    
    Task<int> BulkUpdatePricesAsync(
        List<Guid> productIds,
        decimal? price = null,
        decimal? salePrice = null,
        string? adjustmentType = null,
        decimal? adjustmentValue = null);
    
    Task UpdateSalePriceAsync(Guid productId, decimal? salePrice);
}
