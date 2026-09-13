using ECommerce.DTOs;
using ECommerce.Models;
using ECommerce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ECommerce.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<Product>), 200)]
    [ProducesResponseType(typeof(ProductsResponse), 200)]
    public async Task<IActionResult> GetAllProducts(
        [FromQuery] string? category = null,
        [FromQuery] string? search = null,
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? status = null)
    {
        var activeOnly = !User.IsInRole("Admin");

        // If pagination parameters are provided, return paginated response
        if (page.HasValue && pageSize.HasValue)
        {
            var (products, totalCount) = await _productService.GetAllProductsPaginatedAsync(
                category, search, minPrice, maxPrice, page.Value, pageSize.Value, status, activeOnly);
            return Ok(new ProductsResponse
            {
                Products = products,
                TotalCount = totalCount,
                Page = page.Value,
                PageSize = pageSize.Value,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize.Value)
            });
        }

        // Otherwise, return all products (backward compatibility)
        var allProducts = await _productService.GetAllProductsAsync(
            category, search, minPrice, maxPrice, status, activeOnly);
        return Ok(allProducts);
    }

    [HttpGet("new-arrivals")]
    [ProducesResponseType(typeof(List<Product>), 200)]
    public async Task<IActionResult> GetNewArrivals([FromQuery] int limit = 50)
    {
        var products = await _productService.GetNewArrivalsAsync(limit);
        return Ok(products);
    }

    [HttpGet("best-sellers")]
    [ProducesResponseType(typeof(List<Product>), 200)]
    public async Task<IActionResult> GetBestSellers([FromQuery] int limit = 50)
    {
        var products = await _productService.GetBestSellersAsync(limit);
        return Ok(products);
    }

    /// <summary>Storefront lookup by URL slug; legacy GUID still works for admin/bookmarks.</summary>
    [HttpGet("{publicId}")]
    [ProducesResponseType(typeof(Product), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetProduct(string publicId)
    {
        var includeInactive = User.IsInRole("Admin");
        Product? product;
        if (Guid.TryParse(publicId, out var id))
        {
            product = await _productService.GetProductByIdAsync(id, includeInactive);
        }
        else
        {
            product = await _productService.GetProductBySlugAsync(publicId, includeInactive);
        }

        if (product == null)
        {
            return NotFound();
        }

        return Ok(product);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(Product), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request)
    {
        var images = ResolveProductImages(request.Images, request.ImageUrl);
        if (images.Count == 0)
        {
            return BadRequest(new { message = "At least one product image is required" });
        }

        var product = await _productService.CreateProductAsync(
            request.Name,
            request.Description,
            request.Price,
            images,
            request.Category,
            request.Stock,
            colors: request.Colors,
            sizes: request.Sizes,
            colorImages: request.ColorImages,
            customizationType: request.CustomizationType,
            colorSurcharge: request.ColorSurcharge,
            noSurchargeColors: request.NoSurchargeColors,
            customizationPolicy: request.CustomizationPolicy,
            imageObjectPosition: request.ImageObjectPosition,
            isNewArrival: request.IsNewArrival,
            isBestSeller: request.IsBestSeller,
            isFeatured: request.IsFeatured,
            isActive: request.IsActive);

        if (request.SalePrice.HasValue)
        {
            await _productService.UpdateSalePriceAsync(product.Id, request.SalePrice);
            product = await _productService.GetProductByIdAsync(product.Id, includeInactive: true) ?? product;
        }

        return CreatedAtAction(nameof(GetProduct), new { publicId = product.Slug }, product);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(Product), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] UpdateProductRequest request)
    {
        List<string>? images = null;
        if (request.Images != null)
        {
            images = ResolveProductImages(request.Images, null);
        }
        else if (!string.IsNullOrEmpty(request.ImageUrl))
        {
            images = new List<string> { request.ImageUrl };
        }

        var updatedProduct = await _productService.UpdateProductAsync(
            id,
            request.Name,
            request.Description,
            request.Price,
            images,
            request.Category,
            request.Stock,
            colors: request.Colors,
            sizes: request.Sizes,
            colorImages: request.ColorImages,
            customizationType: request.CustomizationType,
            colorSurcharge: request.ColorSurcharge,
            noSurchargeColors: request.NoSurchargeColors,
            customizationPolicy: request.CustomizationPolicy,
            imageObjectPosition: request.ImageObjectPosition,
            isNewArrival: request.IsNewArrival,
            isBestSeller: request.IsBestSeller,
            isFeatured: request.IsFeatured,
            isActive: request.IsActive);

        if (updatedProduct == null)
        {
            return NotFound();
        }

        // Only touch sale price when explicitly set or cleared (omit = leave unchanged)
        if (request.ClearSalePrice || request.SalePrice.HasValue)
        {
            await _productService.UpdateSalePriceAsync(
                id,
                request.ClearSalePrice ? null : request.SalePrice);
            updatedProduct = await _productService.GetProductByIdAsync(id, includeInactive: true);
        }

        return Ok(updatedProduct);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteProduct(Guid id)
    {
        var success = await _productService.DeleteProductAsync(id);
        if (!success)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpPut("bulk-price")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> BulkUpdatePrices([FromBody] BulkPriceUpdateRequest request)
    {
        if (request.ProductIds == null || request.ProductIds.Count == 0)
        {
            return BadRequest(new { message = "Product IDs are required" });
        }

        if (request.Price.HasValue && request.Price.Value < 0)
        {
            return BadRequest(new { message = "Price must be non-negative" });
        }

        if (request.SalePrice.HasValue && request.SalePrice.Value < 0)
        {
            return BadRequest(new { message = "Sale price must be non-negative" });
        }

        var updatedCount = await _productService.BulkUpdatePricesAsync(
            request.ProductIds,
            request.Price,
            request.SalePrice,
            request.AdjustmentType,
            request.AdjustmentValue);

        return Ok(new { message = $"Updated prices for {updatedCount} products", count = updatedCount });
    }

    [HttpPut("{id:guid}/price")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(Product), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateProductPrice(Guid id, [FromBody] UpdatePriceRequest request)
    {
        if (request.Price.HasValue && request.Price.Value < 0)
        {
            return BadRequest(new { message = "Price must be non-negative" });
        }

        if (request.SalePrice.HasValue && request.SalePrice.Value < 0)
        {
            return BadRequest(new { message = "Sale price must be non-negative" });
        }

        var includeInactive = User.IsInRole("Admin");
        var product = await _productService.GetProductByIdAsync(id, includeInactive);
        if (product == null)
        {
            return NotFound();
        }

        var updatedProduct = await _productService.UpdateProductAsync(
            id,
            name: null,
            description: null,
            price: request.Price,
            images: null,
            category: null,
            availableQuantity: null,
            sku: null,
            isActive: null);

        // Update sale price separately if needed
        if (request.SalePrice.HasValue || request.ClearSalePrice)
        {
            await _productService.UpdateSalePriceAsync(id, request.SalePrice);
            updatedProduct = await _productService.GetProductByIdAsync(id, includeInactive: true);
        }

        return Ok(updatedProduct);
    }

    private static List<string> ResolveProductImages(List<string>? images, string? imageUrl)
    {
        var resolved = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (images != null)
        {
            foreach (var url in images)
            {
                var trimmed = url?.Trim();
                if (string.IsNullOrEmpty(trimmed) || !seen.Add(trimmed))
                {
                    continue;
                }

                resolved.Add(trimmed);
            }
        }

        if (resolved.Count == 0 && !string.IsNullOrWhiteSpace(imageUrl))
        {
            resolved.Add(imageUrl.Trim());
        }

        return resolved;
    }
}
