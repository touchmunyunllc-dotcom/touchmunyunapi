using ECommerce.DTOs;
using ECommerce.Models;
using ECommerce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using FluentValidation;

namespace ECommerce.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AddressController : ControllerBase
{
    private readonly IAddressService _addressService;
    private readonly IValidator<CreateAddressRequest> _createAddressValidator;
    private readonly IValidator<UpdateAddressRequest> _updateAddressValidator;

    public AddressController(
        IAddressService addressService,
        IValidator<CreateAddressRequest> createAddressValidator,
        IValidator<UpdateAddressRequest> updateAddressValidator)
    {
        _addressService = addressService;
        _createAddressValidator = createAddressValidator;
        _updateAddressValidator = updateAddressValidator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<AddressResponse>), 200)]
    public async Task<IActionResult> GetUserAddresses()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null || !Guid.TryParse(userId, out var userIdGuid))
        {
            return Unauthorized();
        }

        var addresses = await _addressService.GetUserAddressesAsync(userIdGuid);
        return Ok(addresses.Select(a => MapToResponse(a)).ToList());
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AddressResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetAddressById(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null || !Guid.TryParse(userId, out var userIdGuid))
        {
            return Unauthorized();
        }

        var address = await _addressService.GetAddressByIdAsync(id, userIdGuid);
        if (address == null)
        {
            return NotFound();
        }

        return Ok(MapToResponse(address));
    }

    [HttpPost]
    [ProducesResponseType(typeof(AddressResponse), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CreateAddress([FromBody] CreateAddressRequest request)
    {
        var validationResult = await _createAddressValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.Errors);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null || !Guid.TryParse(userId, out var userIdGuid))
        {
            return Unauthorized();
        }

        var address = await _addressService.CreateAddressAsync(
            userIdGuid,
            request.AddressLine1,
            request.AddressLine2,
            request.City,
            request.State,
            request.PostalCode,
            request.Country,
            request.IsDefault,
            request.Phone);

        return CreatedAtAction(nameof(GetAddressById), new { id = address.Id }, MapToResponse(address));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AddressResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateAddress(Guid id, [FromBody] UpdateAddressRequest request)
    {
        var validationResult = await _updateAddressValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.Errors);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null || !Guid.TryParse(userId, out var userIdGuid))
        {
            return Unauthorized();
        }

        var updatedAddress = await _addressService.UpdateAddressAsync(
            id,
            userIdGuid,
            request.AddressLine1,
            request.AddressLine2,
            request.City,
            request.State,
            request.PostalCode,
            request.Country,
            request.IsDefault,
            request.Phone);

        if (updatedAddress == null)
        {
            return NotFound();
        }

        return Ok(MapToResponse(updatedAddress));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteAddress(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null || !Guid.TryParse(userId, out var userIdGuid))
        {
            return Unauthorized();
        }

        var success = await _addressService.DeleteAddressAsync(id, userIdGuid);
        if (!success)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpPut("{id:guid}/set-default")]
    [ProducesResponseType(typeof(AddressResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> SetDefaultAddress(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null || !Guid.TryParse(userId, out var userIdGuid))
        {
            return Unauthorized();
        }

        var address = await _addressService.SetDefaultAddressAsync(id, userIdGuid);
        if (address == null)
        {
            return NotFound();
        }

        return Ok(MapToResponse(address));
    }

    private AddressResponse MapToResponse(Address address)
    {
        return new AddressResponse(
            address.Id,
            address.AddressLine1,
            address.AddressLine2,
            address.City,
            address.State,
            address.PostalCode,
            address.Country,
            address.Phone,
            address.IsDefault,
            address.CreatedAt);
    }
}
