namespace ECommerce.Services;

/// <summary>
/// Used when Redis connection string is not configured. Avoids connecting to localhost:6379
/// and keeps OTP / cache-dependent code from throwing at startup.
/// </summary>
public class NoOpRedisService : IRedisService
{
    private readonly ILogger<NoOpRedisService> _logger;

    public NoOpRedisService(ILogger<NoOpRedisService> logger)
    {
        _logger = logger;
    }

    public Task<string?> GetAsync(string key)
    {
        _logger.LogDebug("Redis disabled: GetAsync({Key}) skipped", key);
        return Task.FromResult<string?>(null);
    }

    public Task SetAsync(string key, string value, TimeSpan? expiry = null)
    {
        _logger.LogDebug("Redis disabled: SetAsync({Key}) skipped", key);
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(string key)
    {
        return Task.FromResult(false);
    }

    public Task<bool> ExistsAsync(string key)
    {
        return Task.FromResult(false);
    }

    public Task SetHashAsync(string key, string field, string value)
    {
        return Task.CompletedTask;
    }

    public Task<string?> GetHashAsync(string key, string field)
    {
        return Task.FromResult<string?>(null);
    }

    public Task<T?> GetAsync<T>(string key) where T : class
    {
        return Task.FromResult<T?>(null);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class
    {
        return Task.CompletedTask;
    }

    public Task DeleteByPatternAsync(string pattern)
    {
        return Task.CompletedTask;
    }
}
