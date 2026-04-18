using System.Data;
using Dapper;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Services;

/// <summary>
/// Removes stale rows from stripe_hosted_checkout_pending (abandoned Checkout Sessions).
/// Stripe also sends checkout.session.expired; this is a safety net for missed webhooks.
/// </summary>
public sealed class StripeHostedCheckoutCleanupService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<StripeHostedCheckoutCleanupService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private static readonly TimeSpan OlderThan = TimeSpan.FromDays(7);

    public StripeHostedCheckoutCleanupService(
        IServiceProvider services,
        ILogger<StripeHostedCheckoutCleanupService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var connection = scope.ServiceProvider.GetRequiredService<IDbConnection>();
                if (connection.State != System.Data.ConnectionState.Open)
                {
                    connection.Open();
                }

                var cutoff = DateTime.UtcNow - OlderThan;
                var deleted = await connection.ExecuteAsync(
                    "DELETE FROM stripe_hosted_checkout_pending WHERE created_at < @Cutoff",
                    new { Cutoff = cutoff });
                if (deleted > 0)
                {
                    _logger.LogInformation(
                        "Removed {Count} stale stripe_hosted_checkout_pending rows older than {Days} days",
                        deleted,
                        OlderThan.TotalDays);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stripe hosted checkout pending cleanup failed");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
