using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace ECommerce.Services;

public class ImageService : IImageService
{
    private readonly Cloudinary _cloudinary;
    private readonly ILogger<ImageService> _logger;

    public ImageService(IConfiguration configuration, ILogger<ImageService> logger)
    {
        _logger = logger;
        var cloudName = configuration["Cloudinary:CloudName"];
        var apiKey = configuration["Cloudinary:ApiKey"];
        var apiSecret = configuration["Cloudinary:ApiSecret"];

        if (!string.IsNullOrEmpty(cloudName) && !string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(apiSecret))
        {
            var account = new Account(cloudName, apiKey, apiSecret);
            _cloudinary = new Cloudinary(account);
        }
        else
        {
            _cloudinary = null!;
            _logger.LogWarning("Cloudinary credentials not configured");
        }
    }

    public async Task<string> UploadImageAsync(Stream imageStream, string fileName)
    {
        if (_cloudinary == null)
        {
            throw new InvalidOperationException(
                "Image storage is not configured. Set Cloudinary:CloudName, ApiKey, and ApiSecret on the API.");
        }

        try
        {
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(fileName, imageStream),
                Folder = "touchmunyun/products",
                Overwrite = true,
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.Error != null)
            {
                var detail = string.IsNullOrWhiteSpace(uploadResult.Error.Message)
                    ? "Cloudinary rejected the upload."
                    : uploadResult.Error.Message;
                throw new InvalidOperationException(detail);
            }

            if (uploadResult.SecureUrl == null || string.IsNullOrWhiteSpace(uploadResult.SecureUrl.ToString()))
            {
                throw new InvalidOperationException("Cloudinary did not return an image URL.");
            }

            return uploadResult.SecureUrl.ToString();
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading image to Cloudinary");
            throw;
        }
    }

    /// <summary>
    /// Get optimized image URL with transformations for CDN caching
    /// </summary>
    public string GetOptimizedImageUrl(string publicId, int? width = null, int? height = null)
    {
        if (_cloudinary == null || string.IsNullOrEmpty(publicId))
        {
            return "https://via.placeholder.com/300";
        }

        try
        {
            var transformation = new Transformation()
                .Quality("auto")
                .FetchFormat("auto");

            if (width.HasValue)
            {
                transformation = transformation.Width(width.Value);
            }

            if (height.HasValue)
            {
                transformation = transformation.Height(height.Value);
            }

            if (width.HasValue || height.HasValue)
            {
                transformation = transformation.Crop("limit");
            }

            return _cloudinary.Api.UrlImgUp
                .Transform(transformation)
                .BuildUrl(publicId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating optimized image URL");
            return "https://via.placeholder.com/300";
        }
    }

    public async Task<bool> DeleteImageAsync(string publicId)
    {
        if (_cloudinary == null)
        {
            return false;
        }

        try
        {
            var deleteParams = new DeletionParams(publicId);
            var result = await _cloudinary.DestroyAsync(deleteParams);
            return result.Result == "ok";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting image from Cloudinary");
            return false;
        }
    }
}

