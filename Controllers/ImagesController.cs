using ECommerce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class ImagesController : ControllerBase
{
    private const int MaxFileSizeBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    private readonly IImageService _imageService;
    private readonly ILogger<ImagesController> _logger;

    public ImagesController(IImageService imageService, ILogger<ImagesController> logger)
    {
        _imageService = imageService;
        _logger = logger;
    }

    /// <summary>Upload an image to the configured storage (e.g. Cloudinary) and return the public URL.</summary>
    [HttpPost("upload")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Upload(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "No file uploaded." });
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return BadRequest(new { message = "File size exceeds the 5MB limit." });
        }

        if (!AllowedContentTypes.Contains(file.ContentType))
        {
            return BadRequest(new { message = "Unsupported image type." });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            if (!await IsSupportedImageAsync(stream))
            {
                return BadRequest(new { message = "Invalid image file." });
            }

            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            var url = await _imageService.UploadImageAsync(stream, file.FileName);
            return Ok(new { url });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Image upload failed");
            return BadRequest(new { message = "Image upload failed." });
        }
    }

    private static async Task<bool> IsSupportedImageAsync(Stream stream)
    {
        var header = new byte[12];
        var read = await stream.ReadAsync(header, 0, header.Length);
        if (read < 12)
        {
            return false;
        }

        return IsJpeg(header) || IsPng(header) || IsWebp(header);
    }

    private static bool IsJpeg(IReadOnlyList<byte> header)
    {
        return header.Count >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
    }

    private static bool IsPng(IReadOnlyList<byte> header)
    {
        return header.Count >= 8 &&
               header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
               header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A;
    }

    private static bool IsWebp(IReadOnlyList<byte> header)
    {
        return header.Count >= 12 &&
               header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
               header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50;
    }
}
