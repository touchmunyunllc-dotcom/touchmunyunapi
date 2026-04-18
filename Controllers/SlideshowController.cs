using ECommerce.DTOs;
using ECommerce.Models;
using ECommerce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;

namespace ECommerce.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SlideshowController : ControllerBase
{
    private readonly ISlideshowService _slideshowService;
    private readonly IValidator<CreateSlideRequest> _createSlideValidator;
    private readonly IValidator<UpdateSlideRequest> _updateSlideValidator;

    public SlideshowController(
        ISlideshowService slideshowService,
        IValidator<CreateSlideRequest> createSlideValidator,
        IValidator<UpdateSlideRequest> updateSlideValidator)
    {
        _slideshowService = slideshowService;
        _createSlideValidator = createSlideValidator;
        _updateSlideValidator = updateSlideValidator;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(List<Slide>), 200)]
    [ProducesResponseType(typeof(SlidesResponse), 200)]
    public async Task<IActionResult> GetAllSlides([FromQuery] int? page = null, [FromQuery] int? pageSize = null)
    {
        // If pagination parameters are provided, return paginated response
        if (page.HasValue && pageSize.HasValue)
        {
            var (slides, totalCount) = await _slideshowService.GetAllSlidesPaginatedAsync(page.Value, pageSize.Value);
            return Ok(new SlidesResponse
            {
                Slides = slides,
                TotalCount = totalCount,
                Page = page.Value,
                PageSize = pageSize.Value,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize.Value)
            });
        }

        // Otherwise, return all slides (backward compatibility)
        var allSlides = await _slideshowService.GetAllSlidesAsync();
        return Ok(allSlides);
    }

    [HttpGet("active")]
    [ProducesResponseType(typeof(List<Slide>), 200)]
    public async Task<IActionResult> GetActiveSlides()
    {
        var slides = await _slideshowService.GetActiveSlidesAsync();
        return Ok(slides);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(Slide), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetSlideById(Guid id)
    {
        var slide = await _slideshowService.GetSlideByIdAsync(id);
        if (slide == null)
        {
            return NotFound();
        }
        return Ok(slide);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(Slide), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CreateSlide([FromBody] CreateSlideRequest request)
    {
        var validationResult = await _createSlideValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.Errors);
        }

        var slide = await _slideshowService.CreateSlideAsync(
            request.ImageUrl,
            request.Alt,
            request.Title,
            request.Subtitle,
            request.CtaText,
            request.CtaLink,
            request.Order ?? 0,
            request.IsActive ?? true);

        return CreatedAtAction(nameof(GetSlideById), new { id = slide.Id }, slide);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(Slide), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateSlide(Guid id, [FromBody] UpdateSlideRequest request)
    {
        var validationResult = await _updateSlideValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.Errors);
        }

        var updatedSlide = await _slideshowService.UpdateSlideAsync(
            id,
            request.ImageUrl,
            request.Alt,
            request.Title,
            request.Subtitle,
            request.CtaText,
            request.CtaLink,
            request.Order,
            request.IsActive);

        if (updatedSlide == null)
        {
            return NotFound();
        }

        return Ok(updatedSlide);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteSlide(Guid id)
    {
        var success = await _slideshowService.DeleteSlideAsync(id);
        if (!success)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpPut("reorder")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> ReorderSlides([FromBody] ReorderSlidesRequest request)
    {
        if (request.SlideIds == null || request.SlideIds.Count == 0)
        {
            return BadRequest(new { message = "Slide IDs are required" });
        }

        await _slideshowService.ReorderSlidesAsync(request.SlideIds);
        return Ok(new { message = "Slides reordered successfully" });
    }
}

