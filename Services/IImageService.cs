namespace ECommerce.Services;

public interface IImageService
{
    Task<string> UploadImageAsync(Stream imageStream, string fileName);
    Task<bool> DeleteImageAsync(string publicId);
    string GetOptimizedImageUrl(string publicId, int? width = null, int? height = null);
}

