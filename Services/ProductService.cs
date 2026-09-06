using ECommerce.Models;
using System.Data;
using Dapper;
using System.Linq;

namespace ECommerce.Services;

public class ProductService : IProductService
{
    private const string PRODUCTS_CACHE_KEY = "products:all";
    private const string PRODUCTS_CACHE_VERSION_KEY = "products:version";
    private const string PRODUCT_CACHE_KEY_PREFIX = "product:";
    private static readonly TimeSpan CACHE_EXPIRY = TimeSpan.FromMinutes(15);

    // Explicit column mapping for proper Dapper mapping from snake_case to PascalCase
    private const string PRODUCT_SELECT_COLUMNS = @"
        id AS Id,
        name AS Name,
        description AS Description,
        price AS Price,
        sale_price AS SalePrice,
        images AS Images,
        available_quantity AS AvailableQuantity,
        category AS Category,
        sku AS Sku,
        colors AS Colors,
        sizes AS Sizes,
        COALESCE(color_images::text, '{}') AS ColorImagesJson,
        customization_type AS CustomizationType,
        color_surcharge AS ColorSurcharge,
        no_surcharge_colors AS NoSurchargeColors,
        customization_policy AS CustomizationPolicy,
        image_object_position AS ImageObjectPosition,
        is_active AS IsActive,
        is_new_arrival AS IsNewArrival,
        is_best_seller AS IsBestSeller,
        is_featured AS IsFeatured,
        created_at AS CreatedAt,
        updated_at AS UpdatedAt";

    private static void HydrateProducts(IEnumerable<Product> products)
    {
        foreach (var p in products)
        {
            p.HydrateColorImages();
        }
    }

    private static Product? HydrateProduct(Product? product)
    {
        product?.HydrateColorImages();
        return product;
    }

    private static string BuildActiveStatusClause(string? statusFilter, bool activeOnly)
    {
        if (activeOnly)
        {
            return " AND is_active = TRUE";
        }

        return statusFilter?.Trim().ToLowerInvariant() switch
        {
            "inactive" => " AND is_active = FALSE",
            "active" => " AND is_active = TRUE",
            _ => string.Empty,
        };
    }

    private readonly IDbConnection _connection;
    private readonly IRedisService _redisService;

    public ProductService(IDbConnection connection, IRedisService redisService)
    {
        _connection = connection;
        _redisService = redisService;
    }

    public async Task<List<Product>> GetAllProductsAsync(
        string? category = null,
        string? search = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        string? statusFilter = null,
        bool activeOnly = true)
    {
        // If no filters, try to get from cache
        if (activeOnly
            && string.IsNullOrEmpty(category)
            && string.IsNullOrEmpty(search)
            && !minPrice.HasValue
            && !maxPrice.HasValue)
        {
            var version = await GetProductsCacheVersionAsync();
            var cacheKey = $"{PRODUCTS_CACHE_KEY}:{version}";
            var cachedProducts = await _redisService.GetAsync<List<Product>>(cacheKey);
            if (cachedProducts != null)
            {
                HydrateProducts(cachedProducts);
                return cachedProducts;
            }
        }

        // Build SQL query with filters - using explicit column mapping
        var sql = $"SELECT {PRODUCT_SELECT_COLUMNS} FROM products WHERE 1=1{BuildActiveStatusClause(statusFilter, activeOnly)}";
        var parameters = new DynamicParameters();

        if (!string.IsNullOrEmpty(category))
        {
            sql += " AND category = @Category";
            parameters.Add("Category", category);
        }

        if (!string.IsNullOrEmpty(search))
        {
            sql += " AND (name ILIKE @Search OR description ILIKE @Search)";
            parameters.Add("Search", $"%{search}%");
        }

        if (minPrice.HasValue)
        {
            sql += " AND price >= @MinPrice";
            parameters.Add("MinPrice", minPrice.Value);
        }

        if (maxPrice.HasValue)
        {
            sql += " AND price <= @MaxPrice";
            parameters.Add("MaxPrice", maxPrice.Value);
        }

        var products = await _connection.QueryAsync<Product>(sql, parameters);
        var productList = products.ToList();
        HydrateProducts(productList);

        // Cache only if no filters (all products)
        if (activeOnly
            && string.IsNullOrEmpty(category)
            && string.IsNullOrEmpty(search)
            && !minPrice.HasValue
            && !maxPrice.HasValue)
        {
            var version = await GetProductsCacheVersionAsync();
            var cacheKey = $"{PRODUCTS_CACHE_KEY}:{version}";
            await _redisService.SetAsync(cacheKey, productList, CACHE_EXPIRY);
        }

        return productList;
    }

    public async Task<(List<Product> Products, int TotalCount)> GetAllProductsPaginatedAsync(
        string? category = null,
        string? search = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        int page = 1,
        int pageSize = 10,
        string? statusFilter = null,
        bool activeOnly = true)
    {
        var statusClause = BuildActiveStatusClause(statusFilter, activeOnly);
        var sql = $"SELECT {PRODUCT_SELECT_COLUMNS} FROM products WHERE 1=1{statusClause}";
        var parameters = new DynamicParameters();

        if (!string.IsNullOrEmpty(category))
        {
            sql += " AND category = @Category";
            parameters.Add("Category", category);
        }

        if (!string.IsNullOrEmpty(search))
        {
            sql += " AND (name ILIKE @Search OR description ILIKE @Search)";
            parameters.Add("Search", $"%{search}%");
        }

        if (minPrice.HasValue)
        {
            sql += " AND price >= @MinPrice";
            parameters.Add("MinPrice", minPrice.Value);
        }

        if (maxPrice.HasValue)
        {
            sql += " AND price <= @MaxPrice";
            parameters.Add("MaxPrice", maxPrice.Value);
        }

        // Get total count
        var countSql = sql.Replace($"SELECT {PRODUCT_SELECT_COLUMNS}", "SELECT COUNT(*)");
        var totalCount = await _connection.QueryFirstOrDefaultAsync<int>(countSql, parameters);

        // Add pagination
        var offset = (page - 1) * pageSize;
        sql += " ORDER BY created_at DESC LIMIT @PageSize OFFSET @Offset";
        parameters.Add("PageSize", pageSize);
        parameters.Add("Offset", offset);

        var products = (await _connection.QueryAsync<Product>(sql, parameters)).ToList();
        HydrateProducts(products);
        return (products, totalCount);
    }

    public async Task<List<Product>> GetNewArrivalsAsync(int limit = 50)
    {
        var sql = $@"SELECT {PRODUCT_SELECT_COLUMNS} 
                     FROM products 
                     WHERE is_active = TRUE 
                     ORDER BY is_new_arrival DESC, created_at DESC 
                     LIMIT @Limit";
        
        var products = (await _connection.QueryAsync<Product>(sql, new { Limit = limit })).ToList();
        HydrateProducts(products);
        return products;
    }

    public async Task<List<Product>> GetBestSellersAsync(int limit = 50)
    {
        // First, check if there are any non-cancelled orders
        var hasOrders = await _connection.QueryFirstOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM orders WHERE status != 'Cancelled'");
        
        if (hasOrders == 0)
        {
            // No orders yet, return newest products
            return await GetNewArrivalsAsync(limit);
        }
        
        // Get best sellers based on total quantity sold (excluding cancelled orders)
        // Use a simpler query that avoids complex subqueries
        var sql = $@"SELECT 
                        {PRODUCT_SELECT_COLUMNS}
                     FROM products p
                     LEFT JOIN (
                         SELECT 
                             oi.product_id,
                             SUM(oi.quantity) AS total_sold
                         FROM order_items oi
                         INNER JOIN orders o ON oi.order_id = o.id
                         WHERE o.status != 'Cancelled'
                         GROUP BY oi.product_id
                     ) sales ON p.id = sales.product_id
                     WHERE p.is_active = TRUE
                     ORDER BY p.is_best_seller DESC, COALESCE(sales.total_sold, 0) DESC, p.created_at DESC
                     LIMIT @Limit";
        
        var products = (await _connection.QueryAsync<Product>(sql, new { Limit = limit })).ToList();
        HydrateProducts(products);
        return products;
    }

    public async Task<Product?> GetProductByIdAsync(Guid id, bool includeInactive = false)
    {
        if (!includeInactive)
        {
            var version = await GetProductsCacheVersionAsync();
            var cacheKey = $"{PRODUCT_CACHE_KEY_PREFIX}{id}:{version}";
            
            // Try to get from cache
            var cachedProduct = await _redisService.GetAsync<Product>(cacheKey);
            if (cachedProduct != null)
            {
                return HydrateProduct(cachedProduct);
            }
        }

        var activeClause = includeInactive ? string.Empty : " AND is_active = TRUE";
        var product = await _connection.QueryFirstOrDefaultAsync<Product>(
            $"SELECT {PRODUCT_SELECT_COLUMNS} FROM products WHERE id = @Id{activeClause}",
            new { Id = id });

        if (product != null)
        {
            HydrateProduct(product);
            if (!includeInactive)
            {
                var version = await GetProductsCacheVersionAsync();
                var cacheKey = $"{PRODUCT_CACHE_KEY_PREFIX}{id}:{version}";
                await _redisService.SetAsync(cacheKey, product, CACHE_EXPIRY);
            }
        }

        return product;
    }

    public async Task<Product> CreateProductAsync(
        string name,
        string description,
        decimal price,
        List<string> images,
        string category,
        int availableQuantity,
        string? sku = null,
        List<string>? colors = null,
        List<string>? sizes = null,
        Dictionary<string, string>? colorImages = null,
        string? customizationType = null,
        decimal colorSurcharge = 0,
        List<string>? noSurchargeColors = null,
        string? customizationPolicy = null,
        string? imageObjectPosition = null,
        bool isNewArrival = false,
        bool isBestSeller = false,
        bool isFeatured = false,
        bool isActive = true)
    {
        var productId = Guid.NewGuid();
        var colorImagesJson = Product.SerializeColorImages(colorImages);
        var product = new Product
        {
            Id = productId,
            Name = name,
            Description = description,
            Price = price,
            Images = images ?? new List<string>(),
            Category = category,
            AvailableQuantity = availableQuantity,
            Sku = sku,
            Colors = colors ?? new List<string>(),
            Sizes = sizes ?? new List<string>(),
            ColorImagesJson = colorImagesJson,
            CustomizationType = string.IsNullOrWhiteSpace(customizationType) ? null : customizationType.Trim().ToLowerInvariant(),
            ColorSurcharge = colorSurcharge,
            NoSurchargeColors = noSurchargeColors ?? new List<string>(),
            CustomizationPolicy = string.IsNullOrWhiteSpace(customizationPolicy) ? null : customizationPolicy.Trim(),
            ImageObjectPosition = string.IsNullOrWhiteSpace(imageObjectPosition) ? null : imageObjectPosition.Trim(),
            IsActive = isActive,
            IsNewArrival = isNewArrival,
            IsBestSeller = isBestSeller,
            IsFeatured = isFeatured,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        product.HydrateColorImages();

        await _connection.ExecuteAsync(@"
            INSERT INTO products (id, name, description, price, sale_price, images, category, available_quantity, sku, colors, sizes, color_images, customization_type, color_surcharge, no_surcharge_colors, customization_policy, image_object_position, is_active, is_new_arrival, is_best_seller, is_featured, created_at, updated_at)
            VALUES (@Id, @Name, @Description, @Price, @SalePrice, @Images, @Category, @AvailableQuantity, @Sku, @Colors, @Sizes, CAST(@ColorImagesJson AS jsonb), @CustomizationType, @ColorSurcharge, @NoSurchargeColors, @CustomizationPolicy, @ImageObjectPosition, @IsActive, @IsNewArrival, @IsBestSeller, @IsFeatured, @CreatedAt, @UpdatedAt)",
            new
            {
                product.Id,
                product.Name,
                product.Description,
                product.Price,
                SalePrice = (decimal?)null,
                Images = product.Images.ToArray(),
                product.Category,
                product.AvailableQuantity,
                product.Sku,
                Colors = product.Colors.ToArray(),
                Sizes = product.Sizes.ToArray(),
                ColorImagesJson = colorImagesJson,
                product.CustomizationType,
                product.ColorSurcharge,
                NoSurchargeColors = product.NoSurchargeColors.ToArray(),
                product.CustomizationPolicy,
                product.ImageObjectPosition,
                product.IsActive,
                product.IsNewArrival,
                product.IsBestSeller,
                product.IsFeatured,
                product.CreatedAt,
                product.UpdatedAt
            });

        // Invalidate cache
        await InvalidateProductCacheAsync();

        return product;
    }

    public async Task<Product?> UpdateProductAsync(
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
        List<string>? sizes = null,
        Dictionary<string, string>? colorImages = null,
        string? customizationType = null,
        decimal? colorSurcharge = null,
        List<string>? noSurchargeColors = null,
        string? customizationPolicy = null,
        string? imageObjectPosition = null,
        bool? isNewArrival = null,
        bool? isBestSeller = null,
        bool? isFeatured = null)
    {
        var existingProduct = await _connection.QueryFirstOrDefaultAsync<Product>(
            $"SELECT {PRODUCT_SELECT_COLUMNS} FROM products WHERE id = @Id",
            new { Id = id });
        if (existingProduct == null)
        {
            return null;
        }
        HydrateProduct(existingProduct);

        var updateFields = new List<string>();
        var parameters = new DynamicParameters();
        parameters.Add("Id", id);
        parameters.Add("UpdatedAt", DateTime.UtcNow);

        if (name != null)
        {
            updateFields.Add("name = @Name");
            parameters.Add("Name", name);
        }

        if (description != null)
        {
            updateFields.Add("description = @Description");
            parameters.Add("Description", description);
        }

        if (price.HasValue)
        {
            updateFields.Add("price = @Price");
            parameters.Add("Price", price.Value);
        }

        if (images != null)
        {
            updateFields.Add("images = @Images");
            parameters.Add("Images", images.ToArray());
        }

        if (category != null)
        {
            updateFields.Add("category = @Category");
            parameters.Add("Category", category);
        }

        if (availableQuantity.HasValue)
        {
            updateFields.Add("available_quantity = @AvailableQuantity");
            parameters.Add("AvailableQuantity", availableQuantity.Value);
        }

        if (sku != null)
        {
            updateFields.Add("sku = @Sku");
            parameters.Add("Sku", sku);
        }

        if (isActive.HasValue)
        {
            updateFields.Add("is_active = @IsActive");
            parameters.Add("IsActive", isActive.Value);
        }

        if (colors != null)
        {
            updateFields.Add("colors = @Colors");
            parameters.Add("Colors", colors.ToArray());
        }

        if (sizes != null)
        {
            updateFields.Add("sizes = @Sizes");
            parameters.Add("Sizes", sizes.ToArray());
        }

        if (colorImages != null)
        {
            updateFields.Add("color_images = CAST(@ColorImagesJson AS jsonb)");
            parameters.Add("ColorImagesJson", Product.SerializeColorImages(colorImages));
        }

        if (customizationType != null)
        {
            updateFields.Add("customization_type = @CustomizationType");
            parameters.Add(
                "CustomizationType",
                string.IsNullOrWhiteSpace(customizationType) ? null : customizationType.Trim().ToLowerInvariant());
        }

        if (colorSurcharge.HasValue)
        {
            updateFields.Add("color_surcharge = @ColorSurcharge");
            parameters.Add("ColorSurcharge", colorSurcharge.Value);
        }

        if (noSurchargeColors != null)
        {
            updateFields.Add("no_surcharge_colors = @NoSurchargeColors");
            parameters.Add("NoSurchargeColors", noSurchargeColors.ToArray());
        }

        if (customizationPolicy != null)
        {
            updateFields.Add("customization_policy = @CustomizationPolicy");
            parameters.Add(
                "CustomizationPolicy",
                string.IsNullOrWhiteSpace(customizationPolicy) ? null : customizationPolicy.Trim());
        }

        if (imageObjectPosition != null)
        {
            updateFields.Add("image_object_position = @ImageObjectPosition");
            parameters.Add(
                "ImageObjectPosition",
                string.IsNullOrWhiteSpace(imageObjectPosition) ? null : imageObjectPosition.Trim());
        }

        if (isNewArrival.HasValue)
        {
            updateFields.Add("is_new_arrival = @IsNewArrival");
            parameters.Add("IsNewArrival", isNewArrival.Value);
        }

        if (isBestSeller.HasValue)
        {
            updateFields.Add("is_best_seller = @IsBestSeller");
            parameters.Add("IsBestSeller", isBestSeller.Value);
        }

        if (isFeatured.HasValue)
        {
            updateFields.Add("is_featured = @IsFeatured");
            parameters.Add("IsFeatured", isFeatured.Value);
        }

        updateFields.Add("updated_at = @UpdatedAt");

        var sql = $"UPDATE products SET {string.Join(", ", updateFields)} WHERE id = @Id";
        await _connection.ExecuteAsync(sql, parameters);

        var updatedProduct = await _connection.QueryFirstOrDefaultAsync<Product>(
            $"SELECT {PRODUCT_SELECT_COLUMNS} FROM products WHERE id = @Id",
            new { Id = id });
        HydrateProduct(updatedProduct);

        // Invalidate cache
        await InvalidateProductCacheAsync(id);

        return updatedProduct;
    }

    public async Task<bool> DeleteProductAsync(Guid id)
    {
        if (!await ProductExistsAsync(id))
        {
            return false;
        }

        // Soft delete by setting is_active to false
        await _connection.ExecuteAsync(
            "UPDATE products SET is_active = FALSE, updated_at = CURRENT_TIMESTAMP WHERE id = @Id",
            new { Id = id });

        // Invalidate cache
        await InvalidateProductCacheAsync(id);

        return true;
    }

    public async Task<bool> ProductExistsAsync(Guid id)
    {
        var product = await _connection.QueryFirstOrDefaultAsync<Product>(
            "SELECT id FROM products WHERE id = @Id",
            new { Id = id });
        return product != null;
    }

    public async Task<int> BulkUpdatePricesAsync(
        List<Guid> productIds,
        decimal? price = null,
        decimal? salePrice = null,
        string? adjustmentType = null,
        decimal? adjustmentValue = null)
    {
        if (productIds == null || productIds.Count == 0)
        {
            return 0;
        }

        var updateFields = new List<string>();
        var parameters = new DynamicParameters();
        parameters.Add("UpdatedAt", DateTime.UtcNow);
        parameters.Add("ProductIds", productIds.ToArray());

        // Handle direct price setting
        if (price.HasValue)
        {
            updateFields.Add("price = @Price");
            parameters.Add("Price", price.Value);
        }

        // Handle sale price
        if (salePrice.HasValue)
        {
            updateFields.Add("sale_price = @SalePrice");
            parameters.Add("SalePrice", salePrice.Value);
        }
        else if (salePrice == null && updateFields.Any(f => f.Contains("sale_price")))
        {
            // Clear sale price
            updateFields.Add("sale_price = NULL");
        }

        // Handle percentage or fixed amount adjustments
        if (!string.IsNullOrEmpty(adjustmentType) && adjustmentValue.HasValue)
        {
            if (adjustmentType.ToLower() == "percentage")
            {
                if (price.HasValue)
                {
                    // Adjust the set price
                    updateFields.Remove("price = @Price");
                    updateFields.Add("price = @Price * (1 + @AdjustmentValue / 100)");
                    parameters.Add("AdjustmentValue", adjustmentValue.Value);
                }
                else
                {
                    // Adjust existing prices
                    updateFields.Add("price = price * (1 + @AdjustmentValue / 100)");
                    parameters.Add("AdjustmentValue", adjustmentValue.Value);
                }
            }
            else if (adjustmentType.ToLower() == "fixed")
            {
                if (price.HasValue)
                {
                    updateFields.Remove("price = @Price");
                    updateFields.Add("price = @Price + @AdjustmentValue");
                    parameters.Add("AdjustmentValue", adjustmentValue.Value);
                }
                else
                {
                    updateFields.Add("price = price + @AdjustmentValue");
                    parameters.Add("AdjustmentValue", adjustmentValue.Value);
                }
            }
        }

        if (updateFields.Count == 0)
        {
            return 0;
        }

        updateFields.Add("updated_at = @UpdatedAt");

        // Build SQL with IN clause for multiple product IDs
        var sql = $@"
            UPDATE products 
            SET {string.Join(", ", updateFields)} 
            WHERE id = ANY(@ProductIds)";

        var rowsAffected = await _connection.ExecuteAsync(sql, parameters);

        // Invalidate cache for all affected products
        await InvalidateProductCacheAsync();

        return rowsAffected;
    }

    public async Task UpdateSalePriceAsync(Guid productId, decimal? salePrice)
    {
        var parameters = new DynamicParameters();
        parameters.Add("Id", productId);
        parameters.Add("UpdatedAt", DateTime.UtcNow);

        if (salePrice.HasValue)
        {
            parameters.Add("SalePrice", salePrice.Value);
            await _connection.ExecuteAsync(@"
                UPDATE products 
                SET sale_price = @SalePrice, updated_at = @UpdatedAt 
                WHERE id = @Id AND (@SalePrice IS NULL OR @SalePrice < price)",
                parameters);
        }
        else
        {
            await _connection.ExecuteAsync(@"
                UPDATE products 
                SET sale_price = NULL, updated_at = @UpdatedAt 
                WHERE id = @Id",
                parameters);
        }

        // Invalidate cache
        await InvalidateProductCacheAsync(productId);
    }

    public async Task InvalidateProductCacheAsync(Guid? productId = null)
    {
        await BumpProductsCacheVersionAsync();
    }

    private async Task<string> GetProductsCacheVersionAsync()
    {
        var version = await _redisService.GetAsync(PRODUCTS_CACHE_VERSION_KEY);
        if (!string.IsNullOrWhiteSpace(version))
        {
            return version;
        }

        version = Guid.NewGuid().ToString("N");
        await _redisService.SetAsync(PRODUCTS_CACHE_VERSION_KEY, version);
        return version;
    }

    private async Task BumpProductsCacheVersionAsync()
    {
        var version = Guid.NewGuid().ToString("N");
        await _redisService.SetAsync(PRODUCTS_CACHE_VERSION_KEY, version);
    }
}
