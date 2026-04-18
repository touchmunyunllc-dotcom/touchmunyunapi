using System.Net;
using Microsoft.AspNetCore.Http;

namespace ECommerce.Utils;

/// <summary>
/// Resolves client IP for rate limiting and reCAPTCHA. Use <c>UseForwardedHeaders()</c> in the pipeline so
/// <see cref="HttpContext.Connection.RemoteIpAddress"/> reflects the client; this adds a fallback on X-Forwarded-For / X-Real-IP when present.
/// </summary>
public static class ClientIpHelper
{
    public static string GetClientIpAddress(HttpContext httpContext)
    {
        var xff = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(xff))
        {
            var first = xff.Split(',')[0].Trim();
            if (IPAddress.TryParse(first, out _))
                return first;
        }

        var realIp = httpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(realIp))
        {
            var trimmed = realIp.Trim();
            if (IPAddress.TryParse(trimmed, out _))
                return trimmed;
        }

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
