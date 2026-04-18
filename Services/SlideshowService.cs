using ECommerce.Models;
using System.Data;
using Dapper;

namespace ECommerce.Services;

public class SlideshowService : ISlideshowService
{
    private readonly IDbConnection _connection;

    public SlideshowService(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<List<Slide>> GetAllSlidesAsync()
    {
        var slides = await _connection.QueryAsync<Slide>(
            @"SELECT id AS Id, image_url AS ImageUrl, alt AS Alt, title AS Title, 
                     subtitle AS Subtitle, cta_text AS CtaText, cta_link AS CtaLink, 
                     ""order"" AS ""Order"", is_active AS IsActive, 
                     created_at AS CreatedAt, updated_at AS UpdatedAt 
              FROM slideshow ORDER BY ""order"" ASC, created_at DESC");
        return slides.ToList();
    }

    public async Task<(List<Slide> Slides, int TotalCount)> GetAllSlidesPaginatedAsync(int page = 1, int pageSize = 10)
    {
        // Get total count
        var totalCount = await _connection.QueryFirstOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM slideshow");

        // Get paginated slides
        var offset = (page - 1) * pageSize;
        var slides = await _connection.QueryAsync<Slide>(
            @"SELECT id AS Id, image_url AS ImageUrl, alt AS Alt, title AS Title, 
                     subtitle AS Subtitle, cta_text AS CtaText, cta_link AS CtaLink, 
                     ""order"" AS ""Order"", is_active AS IsActive, 
                     created_at AS CreatedAt, updated_at AS UpdatedAt 
              FROM slideshow ORDER BY ""order"" ASC, created_at DESC 
              LIMIT @PageSize OFFSET @Offset",
            new { PageSize = pageSize, Offset = offset });
        
        return (slides.ToList(), totalCount);
    }

    public async Task<List<Slide>> GetActiveSlidesAsync()
    {
        var slides = await _connection.QueryAsync<Slide>(
            @"SELECT id AS Id, image_url AS ImageUrl, alt AS Alt, title AS Title, 
                     subtitle AS Subtitle, cta_text AS CtaText, cta_link AS CtaLink, 
                     ""order"" AS ""Order"", is_active AS IsActive, 
                     created_at AS CreatedAt, updated_at AS UpdatedAt 
              FROM slideshow WHERE is_active = TRUE ORDER BY ""order"" ASC, created_at DESC");
        return slides.ToList();
    }

    public async Task<Slide?> GetSlideByIdAsync(Guid id)
    {
        return await _connection.QueryFirstOrDefaultAsync<Slide>(
            @"SELECT id AS Id, image_url AS ImageUrl, alt AS Alt, title AS Title, 
                     subtitle AS Subtitle, cta_text AS CtaText, cta_link AS CtaLink, 
                     ""order"" AS ""Order"", is_active AS IsActive, 
                     created_at AS CreatedAt, updated_at AS UpdatedAt 
              FROM slideshow WHERE id = @Id",
            new { Id = id });
    }

    public async Task<Slide> CreateSlideAsync(
        string imageUrl,
        string alt,
        string? title = null,
        string? subtitle = null,
        string? ctaText = null,
        string? ctaLink = null,
        int order = 0,
        bool isActive = true)
    {
        var slideId = Guid.NewGuid();
        var slide = new Slide
        {
            Id = slideId,
            ImageUrl = imageUrl,
            Alt = alt,
            Title = title,
            Subtitle = subtitle,
            CtaText = ctaText,
            CtaLink = ctaLink,
            Order = order,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        };

        await _connection.ExecuteAsync(@"
            INSERT INTO slideshow (id, image_url, alt, title, subtitle, cta_text, cta_link, ""order"", is_active, created_at)
            VALUES (@Id, @ImageUrl, @Alt, @Title, @Subtitle, @CtaText, @CtaLink, @Order, @IsActive, @CreatedAt)",
            new
            {
                slide.Id,
                ImageUrl = slide.ImageUrl,
                Alt = slide.Alt,
                Title = slide.Title,
                Subtitle = slide.Subtitle,
                CtaText = slide.CtaText,
                CtaLink = slide.CtaLink,
                Order = slide.Order,
                IsActive = slide.IsActive,
                CreatedAt = slide.CreatedAt
            });

        return slide;
    }

    public async Task<Slide?> UpdateSlideAsync(
        Guid id,
        string? imageUrl = null,
        string? alt = null,
        string? title = null,
        string? subtitle = null,
        string? ctaText = null,
        string? ctaLink = null,
        int? order = null,
        bool? isActive = null)
    {
        var existingSlide = await GetSlideByIdAsync(id);
        if (existingSlide == null)
        {
            return null;
        }

        var updateFields = new List<string>();
        var parameters = new DynamicParameters();
        parameters.Add("Id", id);
        parameters.Add("UpdatedAt", DateTime.UtcNow);

        if (imageUrl != null)
        {
            updateFields.Add("image_url = @ImageUrl");
            parameters.Add("ImageUrl", imageUrl);
        }

        if (alt != null)
        {
            updateFields.Add("alt = @Alt");
            parameters.Add("Alt", alt);
        }

        if (title != null)
        {
            updateFields.Add("title = @Title");
            parameters.Add("Title", title);
        }

        if (subtitle != null)
        {
            updateFields.Add("subtitle = @Subtitle");
            parameters.Add("Subtitle", subtitle);
        }

        if (ctaText != null)
        {
            updateFields.Add("cta_text = @CtaText");
            parameters.Add("CtaText", ctaText);
        }

        if (ctaLink != null)
        {
            updateFields.Add("cta_link = @CtaLink");
            parameters.Add("CtaLink", ctaLink);
        }

        if (order.HasValue)
        {
            updateFields.Add("\"order\" = @Order");
            parameters.Add("Order", order.Value);
        }

        if (isActive.HasValue)
        {
            updateFields.Add("is_active = @IsActive");
            parameters.Add("IsActive", isActive.Value);
        }

        if (updateFields.Count == 0)
        {
            return existingSlide;
        }

        updateFields.Add("updated_at = @UpdatedAt");

        var sql = $"UPDATE slideshow SET {string.Join(", ", updateFields)} WHERE id = @Id";
        await _connection.ExecuteAsync(sql, parameters);

        return await GetSlideByIdAsync(id);
    }

    public async Task<bool> DeleteSlideAsync(Guid id)
    {
        var slide = await GetSlideByIdAsync(id);
        if (slide == null)
        {
            return false;
        }

        await _connection.ExecuteAsync("DELETE FROM slideshow WHERE id = @Id", new { Id = id });
        return true;
    }

    public async Task ReorderSlidesAsync(List<Guid> slideIds)
    {
        using var transaction = _connection.BeginTransaction();
        try
        {
            for (int i = 0; i < slideIds.Count; i++)
            {
                await _connection.ExecuteAsync(
                    "UPDATE slideshow SET \"order\" = @Order WHERE id = @Id",
                    new { Order = i, Id = slideIds[i] },
                    transaction);
            }
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}

