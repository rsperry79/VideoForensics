using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using System.Threading.RateLimiting;

using VideoForensics.Client.WebApp.Api;
using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Api
{
    /// <summary>
    /// Startup regression for the device-control routes. Handler tests call the static handlers directly, so they
    /// never exercise minimal-API parameter inference. Starting the host with the routes mapped does.
    /// </summary>
    public class DeviceControlEndpointsStartupTests
    {
        [Fact]
        public async Task DeviceControlEndpoints_HostStarts_WithRoutesMapped()
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            _ = builder.WebHost.UseTestServer();

            _ = builder.Services.AddSingleton(new Mock<IDeviceRepository>().Object);
            _ = builder.Services.AddSingleton(new Mock<IDeviceControlService>().Object);
            _ = builder.Services.AddAuthorization(o => o.AddVideoForensicsPolicies());
            _ = builder.Services.AddRateLimiter(options =>
            {
                _ = options.AddPolicy("media", _ => RateLimitPartition.GetNoLimiter("test"));
            });

            await using WebApplication app = builder.Build();
            _ = app.UseRateLimiter();
            _ = app.UseAuthorization();
            app.MapDeviceControlEndpoints();

            // Route-parameter inference runs while the endpoints are built, so a bad handler signature throws here.
            await app.StartAsync();
            await app.StopAsync();
        }
    }
}
