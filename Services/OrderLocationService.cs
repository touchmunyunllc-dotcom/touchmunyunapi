using System.Data;
using Dapper;
using ECommerce.Configuration;
using ECommerce.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ECommerce.Services;

public class OrderLocationService : IOrderLocationService
{
    private const int GeocodeMaxAttempts = 3;
    private readonly IDbConnection _connection;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderLocationService> _logger;

    public OrderLocationService(
        IDbConnection connection,
        IServiceScopeFactory scopeFactory,
        ILogger<OrderLocationService> logger)
    {
        _connection = connection;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task SaveCheckoutCoordinatesAsync(
        Guid orderId,
        decimal? checkoutLatitude,
        decimal? checkoutLongitude,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        if (checkoutLatitude is null && checkoutLongitude is null)
        {
            return;
        }

        if (checkoutLatitude is null || checkoutLongitude is null)
        {
            return;
        }

        var capturedAt = DateTime.UtcNow;
        await _connection.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE orders SET
                    checkout_latitude = @CheckoutLatitude,
                    checkout_longitude = @CheckoutLongitude,
                    checkout_location_at = @CheckoutLocationAt,
                    updated_at = CURRENT_TIMESTAMP
                  WHERE id = @OrderId",
                new
                {
                    OrderId = orderId,
                    CheckoutLatitude = checkoutLatitude,
                    CheckoutLongitude = checkoutLongitude,
                    CheckoutLocationAt = capturedAt
                },
                transaction,
                cancellationToken: cancellationToken));
    }

    public void ScheduleShippingGeocodeForOrder(Guid orderId, Guid? shippingAddressId)
    {
        if (!shippingAddressId.HasValue)
        {
            return;
        }

        var addressId = shippingAddressId.Value;
        RunBackgroundAsync(async sp =>
        {
            await EnrichOrderShippingAsync(sp, orderId, addressId);
        });
    }

    public void ScheduleAddressGeocode(Guid addressId)
    {
        RunBackgroundAsync(async sp =>
        {
            await GeocodeAddressRowAsync(sp, addressId, orderIdToUpdate: null);
        });
    }

    private void RunBackgroundAsync(Func<IServiceProvider, Task> work)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await work(scope.ServiceProvider);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Background order/address geocoding failed");
            }
        });
    }

    private static async Task EnrichOrderShippingAsync(IServiceProvider sp, Guid orderId, Guid addressId)
    {
        var logger = sp.GetRequiredService<ILogger<OrderLocationService>>();
        var (lat, lng) = await GeocodeAddressRowAsync(sp, addressId, orderId);
        if (lat is null || lng is null)
        {
            logger.LogInformation("No shipping coordinates for order {OrderId} (address {AddressId})", orderId, addressId);
            return;
        }

        logger.LogInformation(
            "Order {OrderId} shipping coordinates set to ({Lat},{Lng}) from address {AddressId}",
            orderId, lat, lng, addressId);
    }

    private static async Task<(decimal? Latitude, decimal? Longitude)> GeocodeAddressRowAsync(
        IServiceProvider sp,
        Guid addressId,
        Guid? orderIdToUpdate)
    {
        var connection = sp.GetRequiredService<IDbConnection>();
        var geocoding = sp.GetRequiredService<IGeocodingService>();
        var logger = sp.GetRequiredService<ILogger<OrderLocationService>>();

        var addr = await connection.QueryFirstOrDefaultAsync<AddressGeoRow>(
            @"SELECT id, address_line1, address_line2, city, state, postal_code, country,
                     latitude, longitude, geocoded_at
              FROM addresses WHERE id = @Id",
            new { Id = addressId });

        if (addr is null)
        {
            return (null, null);
        }

        if (addr.Latitude is not null && addr.Longitude is not null)
        {
            if (orderIdToUpdate.HasValue)
            {
                await ApplyShippingToOrderAsync(
                    sp,
                    connection,
                    orderIdToUpdate.Value,
                    addr.Latitude.Value,
                    addr.Longitude.Value);
            }

            return (addr.Latitude, addr.Longitude);
        }

        decimal? lat = null;
        decimal? lng = null;
        for (var attempt = 1; attempt <= GeocodeMaxAttempts; attempt++)
        {
            (lat, lng) = await geocoding.GeocodeAddressAsync(
                addr.AddressLine1,
                addr.AddressLine2,
                addr.City,
                addr.State,
                addr.PostalCode,
                addr.Country);

            if (lat is not null && lng is not null)
            {
                break;
            }

            if (attempt < GeocodeMaxAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2));
            }
        }

        if (lat is null || lng is null)
        {
            return (null, null);
        }

        var geocodedAt = DateTime.UtcNow;
        await connection.ExecuteAsync(
            @"UPDATE addresses SET latitude = @Lat, longitude = @Lng, geocoded_at = @GeocodedAt, updated_at = CURRENT_TIMESTAMP
              WHERE id = @Id",
            new { Id = addressId, Lat = lat, Lng = lng, GeocodedAt = geocodedAt });

        if (orderIdToUpdate.HasValue)
        {
            await ApplyShippingToOrderAsync(sp, connection, orderIdToUpdate.Value, lat.Value, lng.Value);
        }

        return (lat, lng);
    }

    private static async Task ApplyShippingToOrderAsync(
        IServiceProvider sp,
        IDbConnection connection,
        Guid orderId,
        decimal lat,
        decimal lng)
    {
        var options = sp.GetRequiredService<IOptions<GeocodingOptions>>().Value;
        var checkout = await connection.QueryFirstOrDefaultAsync<CheckoutCoordRow>(
            @"SELECT checkout_latitude AS CheckoutLatitude, checkout_longitude AS CheckoutLongitude
              FROM orders WHERE id = @OrderId",
            new { OrderId = orderId });

        decimal? distanceKm = null;
        var mismatch = false;
        if (checkout?.CheckoutLatitude is not null && checkout.CheckoutLongitude is not null)
        {
            distanceKm = (decimal?)GeoDistance.HaversineKilometers(
                checkout.CheckoutLatitude,
                checkout.CheckoutLongitude,
                lat,
                lng);
            if (distanceKm.HasValue && (double)distanceKm.Value > options.MismatchDistanceKm)
            {
                mismatch = true;
            }
        }

        var geocodedAt = DateTime.UtcNow;
        await connection.ExecuteAsync(
            @"UPDATE orders SET
                shipping_latitude = @Lat,
                shipping_longitude = @Lng,
                shipping_geocoded_at = @GeocodedAt,
                location_distance_km = @DistanceKm,
                location_mismatch_flag = @Mismatch,
                updated_at = CURRENT_TIMESTAMP
              WHERE id = @OrderId",
            new
            {
                OrderId = orderId,
                Lat = lat,
                Lng = lng,
                GeocodedAt = geocodedAt,
                DistanceKm = distanceKm,
                Mismatch = mismatch
            });
    }

    private sealed class CheckoutCoordRow
    {
        public decimal? CheckoutLatitude { get; set; }
        public decimal? CheckoutLongitude { get; set; }
    }

    private sealed class AddressGeoRow
    {
        public Guid Id { get; set; }
        public string AddressLine1 { get; set; } = string.Empty;
        public string? AddressLine2 { get; set; }
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public DateTime? GeocodedAt { get; set; }
    }
}
