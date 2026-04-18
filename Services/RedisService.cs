using StackExchange.Redis;
using System.Text.Json;

namespace ECommerce.Services;

public class RedisService : IRedisService
{
    private readonly IDatabase _database;
    private readonly ILogger<RedisService> _logger;
    private readonly IServer _server;

    public RedisService(IConnectionMultiplexer redis, ILogger<RedisService> logger)
    {
        _database = redis.GetDatabase();
        _logger = logger;
        _server = redis.GetServer(redis.GetEndPoints().First());
    }

    public async Task<string?> GetAsync(string key)
    {
        try
        {
            var value = await _database.StringGetAsync(key);
            return value.HasValue ? value.ToString() : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting key {Key} from Redis", key);
            return null;
        }
    }

    public async Task SetAsync(string key, string value, TimeSpan? expiry = null)
    {
        try
        {
            await _database.StringSetAsync(key, value, expiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting key {Key} in Redis", key);
        }
    }

    public async Task<bool> DeleteAsync(string key)
    {
        try
        {
            return await _database.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting key {Key} from Redis", key);
            return false;
        }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        try
        {
            return await _database.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking key {Key} in Redis", key);
            return false;
        }
    }

    public async Task SetHashAsync(string key, string field, string value)
    {
        try
        {
            await _database.HashSetAsync(key, field, value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting hash {Key}.{Field} in Redis", key, field);
        }
    }

    public async Task<string?> GetHashAsync(string key, string field)
    {
        try
        {
            var value = await _database.HashGetAsync(key, field);
            return value.HasValue ? value.ToString() : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting hash {Key}.{Field} from Redis", key, field);
            return null;
        }
    }

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        try
        {
            var value = await _database.StringGetAsync(key);
            if (!value.HasValue)
            {
                return null;
            }

            return JsonSerializer.Deserialize<T>(value.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting object {Key} from Redis", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class
    {
        try
        {
            var json = JsonSerializer.Serialize(value);
            await _database.StringSetAsync(key, json, expiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting object {Key} in Redis", key);
        }
    }

    public async Task DeleteByPatternAsync(string pattern)
    {
        try
        {
            var keys = _server.Keys(pattern: pattern).ToArray();
            if (keys.Length > 0)
            {
                await _database.KeyDeleteAsync(keys);
                _logger.LogInformation("Deleted {Count} keys matching pattern {Pattern}", keys.Length, pattern);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting keys by pattern {Pattern}", pattern);
        }
    }
}

