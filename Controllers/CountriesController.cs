using ECommerce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Controllers;

[ApiController]
[Route("api/countries")]
public class CountriesController : ControllerBase
{
    private readonly ICountryService _countries;

    public CountriesController(ICountryService countries)
    {
        _countries = countries;
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<CountryDto>), 200)]
    public async Task<IActionResult> List()
    {
        var list = await _countries.GetAllAsync();
        return Ok(list.Select(c => new CountryDto(c.Code, c.Name)));
    }
}

public record CountryDto(string Code, string Name);
