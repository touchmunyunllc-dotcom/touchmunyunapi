using ECommerce.DTOs;
using ECommerce.Services;
using ECommerce.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using FluentValidation;

namespace ECommerce.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GuestController : ControllerBase
{
    private readonly IGuestService _guestService;
    private readonly IValidator<GuestCheckoutRequest> _guestCheckoutValidator;
    private readonly IValidator<GuestCheckoutPreviewRequest> _previewValidator;
    private readonly IRecaptchaService _recaptchaService;
    private readonly ICountryService _countryService;

    public GuestController(
        IGuestService guestService,
        IValidator<GuestCheckoutRequest> guestCheckoutValidator,
        IValidator<GuestCheckoutPreviewRequest> previewValidator,
        IRecaptchaService recaptchaService,
        ICountryService countryService)
    {
        _guestService = guestService;
        _guestCheckoutValidator = guestCheckoutValidator;
        _previewValidator = previewValidator;
        _recaptchaService = recaptchaService;
        _countryService = countryService;
    }

    /// <summary>Server-computed totals for guest cart (use this total in POST checkout).</summary>
    [HttpPost("preview-total")]
    [EnableRateLimiting("fixed")]
    [ProducesResponseType(typeof(GuestCheckoutPreviewResponse), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> PreviewGuestTotal([FromBody] GuestCheckoutPreviewRequest request)
    {
        var validationResult = await _previewValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.Errors);
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(request.ShippingCountry))
            {
                var previewCode = CountryCodeHelper.NormalizeToIsoCode(request.ShippingCountry);
                if (!await _countryService.ExistsAsync(previewCode))
                {
                    return BadRequest(new { message = "Invalid country code." });
                }

                request = request with { ShippingCountry = previewCode };
            }

            var preview = await _guestService.PreviewGuestCheckoutAsync(
                request.Items,
                request.CouponCode,
                request.ShippingCountry);
            return Ok(preview);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("checkout")]
    [EnableRateLimiting("strict")]
    [ProducesResponseType(typeof(GuestOrderResponse), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> GuestCheckout([FromBody] GuestCheckoutRequest request)
    {
        var validationResult = await _guestCheckoutValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.Errors);
        }

        var clientIp = ClientIpHelper.GetClientIpAddress(HttpContext);
        var captchaValid = await _recaptchaService.VerifyAsync(request.CaptchaToken, "guest_checkout", clientIp);
        if (!captchaValid)
        {
            return BadRequest(new { message = "CAPTCHA verification failed. Please try again." });
        }

        try
        {
            var countryCode = CountryCodeHelper.NormalizeToIsoCode(request.ShippingAddress.Country);
            if (!await _countryService.ExistsAsync(countryCode))
            {
                return BadRequest(new { message = "Invalid country code." });
            }

            if (!PhoneRules.TryNormalizeToE164(
                    request.ShippingAddress.Phone,
                    countryCode,
                    out var guestPhoneE164,
                    out var guestPhoneError))
            {
                return BadRequest(new { message = guestPhoneError });
            }

            if (!GeoCoordinates.TryValidate(
                    request.CheckoutLatitude,
                    request.CheckoutLongitude,
                    out var geoError))
            {
                return BadRequest(new { message = geoError });
            }

            var result = await _guestService.CreateGuestOrderAsync(
                request.Email,
                request.Name,
                request.Items.Select(i => new GuestOrderItem(
                    i.ProductId, i.Name, i.Price, i.Quantity,
                    i.SelectedColor, i.SelectedSize, i.CustomNumber, i.WritingColor)).ToList(),
                request.TotalAmount,
                request.Currency,
                request.CouponCode,
                new GuestAddress(
                    request.ShippingAddress.AddressLine1,
                    request.ShippingAddress.AddressLine2,
                    request.ShippingAddress.City,
                    request.ShippingAddress.State,
                    request.ShippingAddress.PostalCode,
                    countryCode,
                    guestPhoneE164),
                request.CheckoutLatitude,
                request.CheckoutLongitude);

            return Ok(new GuestOrderResponse(
                result.OrderCode,
                result.ClientSecret,
                result.PaymentIntentId,
                result.OrderId));
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Poll after Stripe payment until webhook creates the order (guest checkout).</summary>
    [HttpGet("checkout-status")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> GuestCheckoutStatus([FromQuery] string paymentIntentId)
    {
        if (string.IsNullOrWhiteSpace(paymentIntentId))
        {
            return BadRequest(new { message = "paymentIntentId is required" });
        }

        var code = await _guestService.GetOrderCodeByPaymentIntentAsync(paymentIntentId.Trim());
        return Ok(new { fulfilled = !string.IsNullOrEmpty(code), orderCode = code });
    }

    [HttpGet("order/{orderCode}")]
    [ProducesResponseType(typeof(GuestOrderResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetGuestOrder(string orderCode)
    {
        var order = await _guestService.GetGuestOrderAsync(orderCode);
        if (order == null)
        {
            return NotFound();
        }

        return Ok(new GuestOrderResponse(
            order.OrderCode,
            null,
            null,
            null,
            order.GuestEmail,
            order.TotalAmount,
            order.Status,
            order.TrackingNumber,
            order.TrackingUrl,
            order.OrderItems));
    }

    [HttpGet("order/{orderCode}/track")]
    [ProducesResponseType(typeof(OrderTrackingResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> TrackGuestOrder(string orderCode)
    {
        var tracking = await _guestService.TrackGuestOrderAsync(orderCode);
        if (tracking == null)
        {
            return NotFound();
        }

        return Ok(new OrderTrackingResponse(
            tracking.OrderCode,
            tracking.Status,
            tracking.TrackingNumber,
            tracking.TrackingUrl,
            tracking.CreatedAt,
            tracking.UpdatedAt));
    }
}
