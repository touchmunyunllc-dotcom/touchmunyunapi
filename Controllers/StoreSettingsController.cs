using ECommerce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Controllers;

[ApiController]
[Route("api/store-settings")]
public class StoreSettingsController : ControllerBase
{
    private readonly IStoreSettingsService _storeSettings;

    public StoreSettingsController(IStoreSettingsService storeSettings)
    {
        _storeSettings = storeSettings;
    }

    /// <summary>Public sales tax rate for storefront estimates.</summary>
    [HttpGet("sales-tax")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SalesTaxSettingsResponse), 200)]
    public async Task<IActionResult> GetSalesTax()
    {
        var rate = await _storeSettings.GetSalesTaxRateAsync();
        return Ok(ToResponse(rate));
    }

    [HttpPut("sales-tax")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(SalesTaxSettingsResponse), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> UpdateSalesTax([FromBody] UpdateSalesTaxRequest request)
    {
        if (request.SalesTaxPercent is < 0 or > 100)
        {
            return BadRequest(new { message = "Sales tax percent must be between 0 and 100." });
        }

        var rate = request.SalesTaxPercent / 100m;
        await _storeSettings.SetSalesTaxRateAsync(rate);
        return Ok(ToResponse(rate));
    }

    /// <summary>Public flat shipping rates (USD).</summary>
    [HttpGet("shipping")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ShippingSettingsResponse), 200)]
    public async Task<IActionResult> GetShipping()
    {
        var standard = await _storeSettings.GetShippingStandardUsdAsync();
        var international = await _storeSettings.GetShippingInternationalUsdAsync();
        return Ok(new ShippingSettingsResponse(standard, international));
    }

    [HttpPut("shipping")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ShippingSettingsResponse), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> UpdateShipping([FromBody] UpdateShippingRequest request)
    {
        if (request.StandardUsd is < 0 or > 10000 || request.InternationalUsd is < 0 or > 10000)
        {
            return BadRequest(new { message = "Shipping amounts must be between 0 and 10000." });
        }

        await _storeSettings.SetShippingRatesAsync(request.StandardUsd, request.InternationalUsd);
        return Ok(new ShippingSettingsResponse(request.StandardUsd, request.InternationalUsd));
    }

    private static SalesTaxSettingsResponse ToResponse(decimal rate) =>
        new(rate, Math.Round(rate * 100m, 4, MidpointRounding.AwayFromZero));
}

public record SalesTaxSettingsResponse(decimal SalesTaxRate, decimal SalesTaxPercent);

public record UpdateSalesTaxRequest(decimal SalesTaxPercent);

public record ShippingSettingsResponse(decimal StandardUsd, decimal InternationalUsd);

public record UpdateShippingRequest(decimal StandardUsd, decimal InternationalUsd);
