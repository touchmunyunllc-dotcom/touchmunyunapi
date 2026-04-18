using ECommerce.Models;
using System.Data;
using Dapper;

namespace ECommerce.Services;

public class AddressService : IAddressService
{
    private readonly IDbConnection _connection;

    public AddressService(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<List<Address>> GetUserAddressesAsync(Guid userId)
    {
        var addresses = await _connection.QueryAsync<Address>(
            "SELECT * FROM addresses WHERE user_id = @UserId ORDER BY is_default DESC, created_at DESC",
            new { UserId = userId });

        return addresses.ToList();
    }

    public async Task<Address?> GetAddressByIdAsync(Guid addressId, Guid userId)
    {
        return await _connection.QueryFirstOrDefaultAsync<Address>(
            "SELECT * FROM addresses WHERE id = @Id AND user_id = @UserId",
            new { Id = addressId, UserId = userId });
    }

    public async Task<Address> CreateAddressAsync(
        Guid userId,
        string addressLine1,
        string? addressLine2,
        string city,
        string state,
        string postalCode,
        string country,
        bool isDefault,
        string? phone = null)
    {
        // If this is set as default, unset other defaults
        if (isDefault)
        {
            await _connection.ExecuteAsync(
                "UPDATE addresses SET is_default = FALSE WHERE user_id = @UserId",
                new { UserId = userId });
        }

        var address = new Address
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AddressLine1 = addressLine1,
            AddressLine2 = addressLine2,
            City = city,
            State = state,
            PostalCode = postalCode,
            Country = country,
            Phone = phone,
            IsDefault = isDefault,
            CreatedAt = DateTime.UtcNow
        };

        await _connection.ExecuteAsync(@"
            INSERT INTO addresses (id, user_id, address_line1, address_line2, city, state, postal_code, country, phone, is_default, created_at)
            VALUES (@Id, @UserId, @AddressLine1, @AddressLine2, @City, @State, @PostalCode, @Country, @Phone, @IsDefault, @CreatedAt)",
            new
            {
                address.Id,
                address.UserId,
                AddressLine1 = address.AddressLine1,
                AddressLine2 = address.AddressLine2,
                address.City,
                address.State,
                PostalCode = address.PostalCode,
                address.Country,
                address.Phone,
                IsDefault = address.IsDefault,
                address.CreatedAt
            });

        return address;
    }

    public async Task<Address?> UpdateAddressAsync(
        Guid addressId,
        Guid userId,
        string? addressLine1 = null,
        string? addressLine2 = null,
        string? city = null,
        string? state = null,
        string? postalCode = null,
        string? country = null,
        bool? isDefault = null,
        string? phone = null)
    {
        var existingAddress = await GetAddressByIdAsync(addressId, userId);
        if (existingAddress == null)
        {
            return null;
        }

        // If setting as default, unset other defaults
        if (isDefault.HasValue && isDefault.Value)
        {
            await _connection.ExecuteAsync(
                "UPDATE addresses SET is_default = FALSE WHERE user_id = @UserId AND id != @Id",
                new { UserId = userId, Id = addressId });
        }

        var updateFields = new List<string>();
        var parameters = new DynamicParameters();
        parameters.Add("Id", addressId);
        parameters.Add("UserId", userId);
        parameters.Add("UpdatedAt", DateTime.UtcNow);

        if (addressLine1 != null)
        {
            updateFields.Add("address_line1 = @AddressLine1");
            parameters.Add("AddressLine1", addressLine1);
        }

        if (addressLine2 != null)
        {
            updateFields.Add("address_line2 = @AddressLine2");
            parameters.Add("AddressLine2", addressLine2);
        }

        if (city != null)
        {
            updateFields.Add("city = @City");
            parameters.Add("City", city);
        }

        if (state != null)
        {
            updateFields.Add("state = @State");
            parameters.Add("State", state);
        }

        if (postalCode != null)
        {
            updateFields.Add("postal_code = @PostalCode");
            parameters.Add("PostalCode", postalCode);
        }

        if (country != null)
        {
            updateFields.Add("country = @Country");
            parameters.Add("Country", country);
        }

        if (phone != null)
        {
            updateFields.Add("phone = @Phone");
            parameters.Add("Phone", phone);
        }

        if (isDefault.HasValue)
        {
            updateFields.Add("is_default = @IsDefault");
            parameters.Add("IsDefault", isDefault.Value);
        }

        if (updateFields.Count == 0)
        {
            return existingAddress; // No changes
        }

        updateFields.Add("updated_at = @UpdatedAt");

        var sql = $"UPDATE addresses SET {string.Join(", ", updateFields)} WHERE id = @Id AND user_id = @UserId";
        await _connection.ExecuteAsync(sql, parameters);

        return await GetAddressByIdAsync(addressId, userId);
    }

    public async Task<bool> DeleteAddressAsync(Guid addressId, Guid userId)
    {
        var address = await GetAddressByIdAsync(addressId, userId);
        if (address == null)
        {
            return false;
        }

        await _connection.ExecuteAsync(
            "DELETE FROM addresses WHERE id = @Id AND user_id = @UserId",
            new { Id = addressId, UserId = userId });

        return true;
    }

    public async Task<Address?> SetDefaultAddressAsync(Guid addressId, Guid userId)
    {
        var address = await GetAddressByIdAsync(addressId, userId);
        if (address == null)
        {
            return null;
        }

        using var transaction = _connection.BeginTransaction();
        try
        {
            // Unset all other defaults
            await _connection.ExecuteAsync(
                "UPDATE addresses SET is_default = FALSE WHERE user_id = @UserId",
                new { UserId = userId },
                transaction);

            // Set this as default
            await _connection.ExecuteAsync(
                "UPDATE addresses SET is_default = TRUE, updated_at = CURRENT_TIMESTAMP WHERE id = @Id",
                new { Id = addressId },
                transaction);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        return await GetAddressByIdAsync(addressId, userId);
    }

    public async Task<bool> AddressExistsAsync(Guid addressId, Guid userId)
    {
        var address = await GetAddressByIdAsync(addressId, userId);
        return address != null;
    }
}

