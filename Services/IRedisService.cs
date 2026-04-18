namespace ECommerce.Services;

public interface IRedisService
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value, TimeSpan? expiry = null);
    Task<bool> DeleteAsync(string key);
    Task<bool> ExistsAsync(string key);
    Task SetHashAsync(string key, string field, string value);
    Task<string?> GetHashAsync(string key, string field);
    
    // Generic methods for object serialization
    Task<T?> GetAsync<T>(string key) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class;
    Task DeleteByPatternAsync(string pattern);
}

