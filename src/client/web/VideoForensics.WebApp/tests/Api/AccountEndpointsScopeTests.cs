using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Moq;

using VideoForensics.Hosting.Services;
using VideoForensics.WebApp.Api;

using Xunit;

namespace VideoForensics.WebApp.Tests.Api
{
    /// <summary>
    /// Regression guard for the root-provider resolution of the scoped <see cref="IEventPullService"/>.
    /// Mapping the endpoints under scope validation must not resolve scoped services from the root.
    /// </summary>
    public class AccountEndpointsScopeTests
    {
        [Fact]
        public void MapAccountEndpoints_ScopedEventPullService_MapsWithoutRootResolution()
        {
            var builder = WebApplication.CreateBuilder();
            builder.Host.UseDefaultServiceProvider(options =>
            {
                options.ValidateScopes = true;
                options.ValidateOnBuild = true;
            });
            _ = builder.Services.AddScoped(_ => Mock.Of<IEventPullService>());

            WebApplication app = builder.Build();

            var ex = Record.Exception(() => app.MapAccountEndpoints());
            Assert.Null(ex);
        }
    }
}
