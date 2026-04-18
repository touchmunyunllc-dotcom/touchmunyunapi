using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace ECommerce.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VersionController : ControllerBase
{
    /// <summary>
    /// Get API version information
    /// </summary>
    [HttpGet]
    public IActionResult GetVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var assemblyVersion = assembly.GetName().Version?.ToString() ?? "1.0.0";
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "1.0.0";
        
        var versionInfo = new
        {
            apiVersion = informationalVersion,
            assemblyVersion = assemblyVersion
        };

        return Ok(versionInfo);
    }

    /// <summary>
    /// Get API health and version (simplified)
    /// </summary>
    [HttpGet("health")]
    public IActionResult GetHealth()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "1.0.0";

        return Ok(new
        {
            status = "healthy",
            version = version,
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
        });
    }
}

