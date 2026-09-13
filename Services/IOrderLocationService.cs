using System.Data;

namespace ECommerce.Services;

public interface IOrderLocationService
{
    /// <summary>Persists device/checkout coordinates only (fast; safe inside DB transactions).</summary>
    Task SaveCheckoutCoordinatesAsync(
        Guid orderId,
        decimal? checkoutLatitude,
        decimal? checkoutLongitude,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    /// <summary>Geocode ship-to address after order commit (background; failures are ignored; never blocks checkout).</summary>
    void ScheduleShippingGeocodeForOrder(Guid orderId, Guid? shippingAddressId);

    /// <summary>Cache geocode on saved addresses (background; never blocks checkout).</summary>
    void ScheduleAddressGeocode(Guid addressId);
}
