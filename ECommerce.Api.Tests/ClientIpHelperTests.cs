using ECommerce.Utils;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace ECommerce.Api.Tests;

public class ClientIpHelperTests
{
    [Fact]
    public void GetClientIpAddress_uses_first_x_forwarded_for_hop()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-Forwarded-For"] = "203.0.113.5, 10.0.0.1";

        var ip = ClientIpHelper.GetClientIpAddress(ctx);

        Assert.Equal("203.0.113.5", ip);
    }

    [Fact]
    public void GetClientIpAddress_falls_back_to_connection_remote_ip()
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.0.2.10");

        var ip = ClientIpHelper.GetClientIpAddress(ctx);

        Assert.Equal("192.0.2.10", ip);
    }
}
