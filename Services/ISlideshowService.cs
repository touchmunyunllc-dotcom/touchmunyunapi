using ECommerce.Models;

namespace ECommerce.Services;

public interface ISlideshowService
{
    Task<List<Slide>> GetAllSlidesAsync();
    Task<(List<Slide> Slides, int TotalCount)> GetAllSlidesPaginatedAsync(int page = 1, int pageSize = 10);
    Task<List<Slide>> GetActiveSlidesAsync();
    Task<Slide?> GetSlideByIdAsync(Guid id);
    Task<Slide> CreateSlideAsync(
        string imageUrl,
        string alt,
        string? title = null,
        string? subtitle = null,
        string? ctaText = null,
        string? ctaLink = null,
        int order = 0,
        bool isActive = true);
    Task<Slide?> UpdateSlideAsync(
        Guid id,
        string? imageUrl = null,
        string? alt = null,
        string? title = null,
        string? subtitle = null,
        string? ctaText = null,
        string? ctaLink = null,
        int? order = null,
        bool? isActive = null);
    Task<bool> DeleteSlideAsync(Guid id);
    Task ReorderSlidesAsync(List<Guid> slideIds);
}

