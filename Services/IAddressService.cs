using ECommerce.Models;

namespace ECommerce.Services;

public interface IAddressService
{
    Task<List<Address>> GetUserAddressesAsync(Guid userId);
    Task<Address?> GetAddressByIdAsync(Guid addressId, Guid userId);
    Task<Address> CreateAddressAsync(
        Guid userId,
        string addressLine1,
        string? addressLine2,
        string city,
        string state,
        string postalCode,
        string country,
        bool isDefault,
        string? phone = null);
    Task<Address?> UpdateAddressAsync(
        Guid addressId,
        Guid userId,
        string? addressLine1 = null,
        string? addressLine2 = null,
        string? city = null,
        string? state = null,
        string? postalCode = null,
        string? country = null,
        bool? isDefault = null,
        string? phone = null);
    Task<bool> DeleteAddressAsync(Guid addressId, Guid userId);
    Task<Address?> SetDefaultAddressAsync(Guid addressId, Guid userId);
    Task<bool> AddressExistsAsync(Guid addressId, Guid userId);
}

