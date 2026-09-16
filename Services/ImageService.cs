using System.Net.Http.Headers;
using System.Text.Json;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace ECommerce.Services;

public class ImageService : IImageService
{
    private const string ProductFolder = "touchmunyun/products";

    private readonly Cloudinary? _cloudinary;
    private readonly string? _cloudName;
    private readonly string? _uploadPreset;
    private readonly ILogger<ImageService> _logger;

    public ImageService(IConfiguration configuration, ILogger<ImageService> logger)
    {
        _logger = logger;
        _cloudName = configuration["Cloudinary:CloudName"]?.Trim();
        var apiKey = configuration["Cloudinary:ApiKey"]?.Trim();
        var apiSecret = configuration["Cloudinary:ApiSecret"]?.Trim();
        _uploadPreset = configuration["Cloudinary:UploadPreset"]?.Trim();

        if (!string.IsNullOrEmpty(_cloudName) && !string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(apiSecret))
        {
            var account = new Account(_cloudName, apiKey, apiSecret);
            _cloudinary = new Cloudinary(account);
        }
        else
        {
            _cloudinary = null;
            if (string.IsNullOrEmpty(_uploadPreset))
            {
                _logger.LogWarning("Cloudinary credentials not configured (need ApiSecret or UploadPreset)");
            }
        }
    }

    public async Task<string> UploadImageAsync(Stream imageStream, string fileName)
    {
        if (!string.IsNullOrEmpty(_uploadPreset) && !string.IsNullOrEmpty(_cloudName))
        {
            return await UploadViaUnsignedPresetAsync(imageStream, fileName);
        }

        if (_cloudinary == null)
        {
            throw new InvalidOperationException(
                "Image storage is not configured. Set Cloudinary:CloudName, ApiKey, and ApiSecret, " +
                "or Cloudinary:CloudName and Cloudinary:UploadPreset (unsigned preset) on the API.");
        }

        try
        {
            return await UploadViaSignedApiAsync(imageStream, fileName);
        }
        catch (InvalidOperationException ex) when (IsInvalidSignature(ex.Message))
        {
            _logger.LogError(
                "Cloudinary Invalid Signature for cloud {CloudName}. Fix Cloudinary__ApiSecret on the API " +
                "or set Cloudinary__UploadPreset to an unsigned preset.",
                _cloudName);
            throw new InvalidOperationException(
                "Cloudinary API secret does not match this cloud (Invalid Signature). " +
                "In Render, set Cloudinary__CloudName, Cloudinary__ApiKey, and Cloudinary__ApiSecret " +
                "from the same row in Cloudinary → Settings → API Keys, or add Cloudinary__UploadPreset " +
                "for an unsigned upload preset.",
                ex);
        }
    }

    private async Task<string> UploadViaSignedApiAsync(Stream imageStream, string fileName)
    {
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(fileName, imageStream),
            Folder = ProductFolder,
            Overwrite = true,
        };

        var uploadResult = await _cloudinary!.UploadAsync(uploadParams);

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

    private async Task<string> UploadViaUnsignedPresetAsync(Stream imageStream, string fileName)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(imageStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(streamContent, "file", fileName);
        content.Add(new StringContent(_uploadPreset!), "upload_preset");
        content.Add(new StringContent(ProductFolder), "folder");

        var endpoint = $"https://api.cloudinary.com/v1_1/{_cloudName}/image/upload";
        using var response = await client.PostAsync(endpoint, content);
        var body = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var error))
        {
            var message = error.TryGetProperty("message", out var msg)
                ? msg.GetString()
                : "Cloudinary rejected the upload.";
            throw new InvalidOperationException(message ?? "Cloudinary rejected the upload.");
        }

        if (!root.TryGetProperty("secure_url", out var urlProp))
        {
            throw new InvalidOperationException("Cloudinary did not return an image URL.");
        }

        var url = urlProp.GetString();
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("Cloudinary did not return an image URL.");
        }

        return url;
    }

    private static bool IsInvalidSignature(string? message)
    {
        return message != null &&
               message.Contains("Invalid Signature", StringComparison.OrdinalIgnoreCase);
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
