using ECommerce.DTOs;
using ECommerce.Models;
using ECommerce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
        [FromQuery] int? pageSize = null)
    {
        // If pagination parameters are provided, return paginated response
        if (page.HasValue && pageSize.HasValue)
        {
            var (products, totalCount) = await _productService.GetAllProductsPaginatedAsync(
                category, search, minPrice, maxPrice, page.Value, pageSize.Value);
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
        var allProducts = await _productService.GetAllProductsAsync(category, search, minPrice, maxPrice);
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

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Product), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetProductById(Guid id)
    {
        var product = await _productService.GetProductByIdAsync(id);
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
        // Convert single ImageUrl to Images list for backward compatibility
        var images = string.IsNullOrEmpty(request.ImageUrl) 
            ? new List<string>() 
            : new List<string> { request.ImageUrl };

        var product = await _productService.CreateProductAsync(
            request.Name,
            request.Description,
            request.Price,
            images,
            request.Category,
            request.Stock,
            colors: request.Colors,
            sizes: request.Sizes);

        return CreatedAtAction(nameof(GetProductById), new { id = product.Id }, product);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(Product), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] UpdateProductRequest request)
    {
        // Convert ImageUrl to Images list if provided
        List<string>? images = null;
        if (!string.IsNullOrEmpty(request.ImageUrl))
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
            sizes: request.Sizes);

        if (updatedProduct == null)
        {
            return NotFound();
        }

        // Update sale price if provided
        if (request.SalePrice.HasValue || (request.SalePrice == null && updatedProduct != null))
        {
            await _productService.UpdateSalePriceAsync(id, request.SalePrice);
            updatedProduct = await _productService.GetProductByIdAsync(id);
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

        var product = await _productService.GetProductByIdAsync(id);
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
            updatedProduct = await _productService.GetProductByIdAsync(id);
        }

        return Ok(updatedProduct);
    }
}
