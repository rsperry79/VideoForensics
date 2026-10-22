using System.Linq;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Moq;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Hosting.Services;
using VideoForensics.WebApp.Api;
using VideoForensics.WebApp.Auth;

using Xunit;

namespace VideoForensics.WebApp.Tests.Api
{
    /// <summary>
    /// Authorization metadata for the accounts routes: every accounts route, including sync-now and config sync,
    /// requires SuperAdmin with the Local tier.
    /// </summary>
    public class AccountEndpointsSyncNowAuthorizationTests
    {
        private const string SyncNowPattern = "/api/v1/accounts/provider-accounts/{id}/sync-now";
        private const string UsersPattern = "/api/v1/accounts/users";
        private const string CreateProviderAccountPattern = "/api/v1/accounts/provider-accounts";
        private const string InitialSyncPattern = "/api/v1/accounts/provider-accounts/{id}/initial-sync";
        private const string ConfigSyncIntervalPattern = "/api/v1/accounts/provider-accounts/{id}/config-sync-interval";

        [Fact]
        public void MapAccountEndpoints_SyncNowRoute_RequiresSuperAdminLocalPolicy()
        {
            IEndpointRouteBuilder app = BuildAndMap();

            RouteEndpoint endpoint = FindSingleEndpoint(app, SyncNowPattern);
            IAuthorizeData[] authorize = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().ToArray();

            Assert.Contains(authorize, a => a.Policy == VideoForensicsPolicies.SuperAdminLocal);
            Assert.DoesNotContain(authorize, a => a.Policy == VideoForensicsPolicies.Admin);
        }

        [Fact]
        public void MapAccountEndpoints_UsersRoute_RequiresSuperAdminLocalPolicy()
        {
            IEndpointRouteBuilder app = BuildAndMap();

            RouteEndpoint[] endpoints = FindEndpoints(app, UsersPattern);
            Assert.NotEmpty(endpoints);
            foreach (RouteEndpoint endpoint in endpoints)
            {
                IAuthorizeData[] authorize = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().ToArray();
                Assert.Contains(authorize, a => a.Policy == VideoForensicsPolicies.SuperAdminLocal);
                Assert.DoesNotContain(authorize, a => a.Policy == VideoForensicsPolicies.SuperAdminLocalOrPasskey);
            }
        }

        [Fact]
        public void MapAccountEndpoints_CreateProviderAccountRoute_RequiresSuperAdminLocalPolicy()
        {
            IEndpointRouteBuilder app = BuildAndMap();

            RouteEndpoint endpoint = FindSingleEndpoint(app, CreateProviderAccountPattern, HttpMethods.Post);
            IAuthorizeData[] authorize = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().ToArray();

            Assert.Contains(authorize, a => a.Policy == VideoForensicsPolicies.SuperAdminLocal);
            Assert.DoesNotContain(authorize, a => a.Policy == VideoForensicsPolicies.SuperAdminLocalOrPasskey);
        }

        [Fact]
        public void MapAccountEndpoints_InitialSyncRoute_RequiresSuperAdminLocalPolicy()
        {
            IEndpointRouteBuilder app = BuildAndMap();

            RouteEndpoint endpoint = FindSingleEndpoint(app, InitialSyncPattern, HttpMethods.Post);
            IAuthorizeData[] authorize = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().ToArray();

            Assert.Contains(authorize, a => a.Policy == VideoForensicsPolicies.SuperAdminLocal);
            Assert.DoesNotContain(authorize, a => a.Policy == VideoForensicsPolicies.SuperAdminLocalOrPasskey);
        }

        [Fact]
        public void MapAccountEndpoints_GetConfigSyncIntervalRoute_RequiresSuperAdminLocalPolicy()
        {
            IEndpointRouteBuilder app = BuildAndMap();

            RouteEndpoint endpoint = FindSingleEndpoint(app, ConfigSyncIntervalPattern, HttpMethods.Get);
            IAuthorizeData[] authorize = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().ToArray();

            Assert.Contains(authorize, a => a.Policy == VideoForensicsPolicies.SuperAdminLocal);
            Assert.DoesNotContain(authorize, a => a.Policy == VideoForensicsPolicies.Admin);
        }

        [Fact]
        public void MapAccountEndpoints_PutConfigSyncIntervalRoute_RequiresSuperAdminLocalPolicy()
        {
            IEndpointRouteBuilder app = BuildAndMap();

            RouteEndpoint endpoint = FindSingleEndpoint(app, ConfigSyncIntervalPattern, HttpMethods.Put);
            IAuthorizeData[] authorize = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().ToArray();

            Assert.Contains(authorize, a => a.Policy == VideoForensicsPolicies.SuperAdminLocal);
            Assert.DoesNotContain(authorize, a => a.Policy == VideoForensicsPolicies.Admin);
        }

        private static IEndpointRouteBuilder BuildAndMap()
        {
            var builder = WebApplication.CreateBuilder();
            builder.Host.UseDefaultServiceProvider(options =>
            {
                options.ValidateScopes = true;
                options.ValidateOnBuild = true;
            });
            _ = builder.Services.AddScoped(_ => Mock.Of<IEventPullService>());

            // Handlers take these as parameters; unregistered ones are inferred as request bodies and mapping throws.
            _ = builder.Services.AddScoped(_ => Mock.Of<IUserRepository>());
            _ = builder.Services.AddScoped(_ => Mock.Of<IProviderAccountRepository>());
            _ = builder.Services.AddScoped(_ => Mock.Of<ICaseRepository>());
            _ = builder.Services.AddScoped(_ => Mock.Of<IProviderSyncRunRepository>());
            _ = builder.Services.AddScoped(_ => Mock.Of<VideoForensics.Client.Common.Contracts.IAccountExportService>());
            _ = builder.Services.AddScoped(_ => Mock.Of<IRingAccountSyncState>());

            WebApplication app = builder.Build();
            app.MapAccountEndpoints();
            return app;
        }

        private static RouteEndpoint[] FindEndpoints(IEndpointRouteBuilder app, string pattern) =>
            app.DataSources
                .SelectMany(d => d.Endpoints)
                .OfType<RouteEndpoint>()
                .Where(e => StripConstraints(e.RoutePattern.RawText) == pattern)
                .ToArray();

        private static RouteEndpoint FindSingleEndpoint(IEndpointRouteBuilder app, string pattern) =>
            FindSingleEndpoint(app, pattern, method: null);

        /// <summary>Finds the one endpoint for a pattern and HTTP method, for patterns shared by GET and PUT (or POST).</summary>
        private static RouteEndpoint FindSingleEndpoint(IEndpointRouteBuilder app, string pattern, string? method)
        {
            RouteEndpoint[] matches = FindEndpoints(app, pattern)
                .Where(e => method is null || e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(method) == true)
                .ToArray();
            Assert.Single(matches);
            return matches[0];
        }

        /// <summary>Removes route constraints so "{id:guid}" compares equal to "{id}".</summary>
        private static string? StripConstraints(string? rawText) =>
            rawText is null ? null : System.Text.RegularExpressions.Regex.Replace(rawText, @"\{(\w+):[^}]+\}", "{$1}");
    }
}
