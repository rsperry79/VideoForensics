using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting;
using VideoForensics.Client.WebApp.Api;
using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Client.WebApp.Services;
using VideoForensics.Data.Common.Contracts;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Api
{
    /// <summary>
    /// Authorization metadata for the provider-account permissions route. Reading the setting is open to Admin and up
    /// so the UI can show whether edits are allowed; changing it is SuperAdmin only, on the same passkey-or-local gate as
    /// the lockout policy it sits beside.
    /// </summary>
    public class ProviderPermissionEndpointsTests
    {
        private const string Pattern = "/api/v1/permissions/provider-accounts";

        [Fact]
        public void MapProviderPermissionEndpoints_PutRoute_RequiresLocalOrPasskeySuperAdmin()
        {
            IEndpointRouteBuilder app = BuildAndMap();

            RouteEndpoint endpoint = FindSingleEndpoint(app, Pattern, HttpMethods.Put);
            IAuthorizeData[] authorize = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().ToArray();

            Assert.Contains(authorize, a => a.Policy == VideoForensicsPolicies.SuperAdminLocalOrPasskey);
        }

        [Fact]
        public void MapProviderPermissionEndpoints_GetRoute_IsOpenToAdminAndUp()
        {
            IEndpointRouteBuilder app = BuildAndMap();

            RouteEndpoint endpoint = FindSingleEndpoint(app, Pattern, HttpMethods.Get);
            IAuthorizeData[] authorize = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().ToArray();

            Assert.Contains(authorize, a => a.Policy == VideoForensicsPolicies.Admin);
            Assert.DoesNotContain(authorize, a => a.Policy == VideoForensicsPolicies.SuperAdminLocalOrPasskey);
        }

        private static IEndpointRouteBuilder BuildAndMap()
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            _ = builder.Host.UseDefaultServiceProvider(options =>
            {
                options.ValidateScopes = true;
                options.ValidateOnBuild = true;
            });

            // Handlers take these as parameters; unregistered ones are inferred as request bodies and mapping throws.
            _ = builder.Services.AddScoped(_ => Mock.Of<IAppSettingRepository>());
            _ = builder.Services.AddScoped(_ => Mock.Of<ISecurityAuditLogger>());
            _ = builder.Services.AddScoped(_ => Mock.Of<INetworkTierResolver>());
            _ = builder.Services.AddScoped(_ => Mock.Of<ISettingsAccessPolicy>());

            WebApplication app = builder.Build();
            app.MapProviderPermissionEndpoints();
            return app;
        }

        private static RouteEndpoint FindSingleEndpoint(IEndpointRouteBuilder app, string pattern, string method)
        {
            RouteEndpoint[] matches = app.DataSources
                .SelectMany(d => d.Endpoints)
                .OfType<RouteEndpoint>()
                .Where(e => e.RoutePattern.RawText == pattern)
                .Where(e => e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(method) == true)
                .ToArray();
            _ = Assert.Single(matches);
            return matches[0];
        }
    }
}
