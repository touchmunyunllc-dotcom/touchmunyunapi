using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace ECommerce.Services;

public class StartupService : IStartupService
{
    private readonly IRedisService _redisService;
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StartupService> _logger;

    // Cache key constants - centralized to avoid duplication
    private const string PRODUCTS_CACHE_KEY = "products:all";
    private const string PRODUCT_CACHE_PATTERN = "product:*";
    private const string COUPONS_CACHE_KEY = "coupons:all";
    private const string COUPON_CACHE_PATTERN = "coupon:*";

    public StartupService(
        IRedisService redisService,
        IHostEnvironment environment,
        IConfiguration configuration,
        ILogger<StartupService> logger)
    {
        _redisService = redisService;
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        await ClearCacheIfNeededAsync();
    }

    private async Task ClearCacheIfNeededAsync()
    {
        // Only clear cache in Development or when explicitly configured
        var shouldClearCache = _environment.IsDevelopment() ||
                              _configuration.GetValue<bool>("Cache:ClearOnStartup", false);

        if (!shouldClearCache)
        {
            _logger.LogInformation("Redis cache preserved on startup (Production mode)");
            return;
        }

        try
        {
            await ClearProductCacheAsync();
            await ClearCouponCacheAsync();

            _logger.LogInformation("✓ Redis cache cleared on startup (Development mode or configured)");
        }
        catch (Exception ex)
        {
            // Log but don't fail startup if Redis is unavailable
            _logger.LogWarning(ex,
                "Failed to clear Redis cache on startup (this is OK if Redis is not configured): {Message}",
                ex.Message);
        }
    }

    private async Task ClearProductCacheAsync()
    {
        await _redisService.DeleteAsync(PRODUCTS_CACHE_KEY);
        await _redisService.DeleteByPatternAsync(PRODUCT_CACHE_PATTERN);
        _logger.LogDebug("Cleared product cache: {Key} and pattern {Pattern}", PRODUCTS_CACHE_KEY, PRODUCT_CACHE_PATTERN);
    }

    private async Task ClearCouponCacheAsync()
    {
        await _redisService.DeleteAsync(COUPONS_CACHE_KEY);
        await _redisService.DeleteByPatternAsync(COUPON_CACHE_PATTERN);
        _logger.LogDebug("Cleared coupon cache: {Key} and pattern {Pattern}", COUPONS_CACHE_KEY, COUPON_CACHE_PATTERN);
    }
}

