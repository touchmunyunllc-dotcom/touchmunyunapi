using ECommerce.Models;

namespace ECommerce.DTOs;

public record CreateSlideRequest(
    string ImageUrl,
    string Alt,
    string? Title = null,
    string? Subtitle = null,
    string? CtaText = null,
    string? CtaLink = null,
    int? Order = null,
    bool? IsActive = null);

public record UpdateSlideRequest(
    string? ImageUrl = null,
    string? Alt = null,
    string? Title = null,
    string? Subtitle = null,
    string? CtaText = null,
    string? CtaLink = null,
    int? Order = null,
    bool? IsActive = null);

public record ReorderSlidesRequest(List<Guid> SlideIds);

public record SlidesResponse
{
    public List<Slide> Slides { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}
