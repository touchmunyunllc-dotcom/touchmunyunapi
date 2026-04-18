namespace ECommerce.DTOs;

public record CreateAddressRequest(
    string AddressLine1,
    string? AddressLine2,
    string City,
    string State,
    string PostalCode,
    string Country,
    bool IsDefault,
    string? Phone);

public record UpdateAddressRequest(
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? PostalCode,
    string? Country,
    bool? IsDefault,
    string? Phone);

public record AddressResponse(
    Guid Id,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string State,
    string PostalCode,
    string Country,
    string? Phone,
    bool IsDefault,
    DateTime CreatedAt);
