using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Moq;

using VideoForensics.Client.WebApp.Api;
using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Client.Hosting.Services;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Api
{
    /// <summary>
    /// Authorization metadata for the accounts routes. Reads and provider-account edits are Admin and up (the edit
    /// setting can narrow edits to SuperAdmin at request time, see ProviderAccountEditFilter). Users, delete, and the
    /// export and purge routes stay SuperAdmin with the Local tier.
    /// </summary>
    public class AccountEndpointsSyncNowAuthorizationTests
    {
        private const string SyncNowPattern = "/api/v1/accounts/provider-accounts/{id}/sync-now";
        private const string UsersPattern = "/api/v1/accounts/users";
        private const string ProviderAccountPattern = "/api/v1/accounts/provider-accounts/{id}";
        private const string CreateProviderAccountPattern = "/api/v1/accounts/provider-accounts";
        private const string InitialSyncPattern = "/api/v1/accounts/provider-accounts/{id}/initial-sync";
        private const string ConfigSyncIntervalPattern = "/api/v1/accounts/provider-accounts/{id}/config-sync-interval";

        [Fact]
        public void MapAccountEndpoints_SyncNowRoute_RequiresAdminNotSuperAdminLocal()
        {
            IEndpointRouteBuilder app = BuildAndMap();

            RouteEndpoint endpoint = FindSingleEndpoint(app, SyncNowPattern);
            IAuthorizeData[] authorize = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().ToArray();

            Assert.Contains(authorize, a => a.Policy == VideoForensicsPolicies.Admin);
            Assert.DoesNotContain(authorize, a => a.Policy == VideoForensicsPolicies.SuperAdminLocal);
        }

        [Fact]
        public void MapAccountEndpoints_UserWriteRoutes_RequireSuperAdminLocalPolicy()
        {
            // User reads are Admin (the account pages list users); only writes are SuperAdmin on the Local tier.
            IEndpointRouteBuilder app = BuildAndMap();

            RouteEndpoint[] endpoints = FindEndpoints(app, UsersPattern)
                .Where(e => e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Any(m => m != HttpMethods.Get) == true)
                .ToArray();
            Assert.NotEmpty(endpoints);
            foreach (RouteEndpoint endpoint in endpoints)
            {
                IAuthorizeData[] authorize = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().ToArray();
                Assert.Contains(authorize, a => a.Policy == VideoForensicsPolicies.SuperAdminLocal);
                Assert.DoesNotContain(authorize, a => a.Policy == VideoForensicsPolicies.SuperAdminLocalOrPasskey);
            }
        }

        [Fact]
        public void MapAccountEndpoints_CreateProviderAccountRoute_RequiresAdminNotSuperAdminLocal()
        {
            IEndpointRouteBuilder app = BuildAndMap();

            RouteEndpoint endpoint = FindSingleEndpoint(app, CreateProviderAccountPattern, HttpMethods.Post);
            IAuthorizeData[] authorize = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().ToArray();

            Assert.Contains(authorize, a => a.Policy == VideoForensicsPolicies.Admin);
            Assert.DoesNotContain(authorize, a => a.Policy == VideoForensicsPolicies.SuperAdminLocal);
        }

        [Fact]
        public void MapAccountEndpoints_InitialSyncRoute_RequiresAdminNotSuperAdminLocal()
        {
            IEndpointRouteBuilder app = BuildAndMap();

            RouteEndpoint endpoint = FindSingleEndpoint(app, InitialSyncPattern, HttpMethods.Post);
            IAuthorizeData[] authorize = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().ToArray();

            Assert.Contains(authorize, a => a.Policy == VideoForensicsPolicies.Admin);
            Assert.DoesNotContain(authorize, a => a.Policy == VideoForensicsPolicies.SuperAdminLocal);
        }

        [Fact]
        public void MapAccountEndpoints_GetConfigSyncIntervalRoute_RequiresAdmin()
        {
            IEndpointRouteBuilder app = BuildAndMap();

            RouteEndpoint endpoint = FindSingleEndpoint(app, ConfigSyncIntervalPattern, HttpMethods.Get);
            IAuthorizeData[] authorize = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().ToArray();

            Assert.Contains(authorize, a => a.Policy == VideoForensicsPolicies.Admin);
            Assert.DoesNotContain(authorize, a => a.Policy == VideoForensicsPolicies.SuperAdminLocal);
        }

        [Fact]
        public void MapAccountEndpoints_PutConfigSyncIntervalRoute_RequiresAdminNotSuperAdminLocal()
        {
            IEndpointRouteBuilder app = BuildAndMap();

            RouteEndpoint endpoint = FindSingleEndpoint(app, ConfigSyncIntervalPattern, HttpMethods.Put);
            IAuthorizeData[] authorize = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().ToArray();

            Assert.Contains(authorize, a => a.Policy == VideoForensicsPolicies.Admin);
            Assert.DoesNotContain(authorize, a => a.Policy == VideoForensicsPolicies.SuperAdminLocal);
        }

        [Fact]
        public void MapAccountEndpoints_DeleteProviderAccountRoute_RequiresSuperAdminLocal()
        {
            // Deleting a provider account is SuperAdmin only, whatever the edit setting says.
            IEndpointRouteBuilder app = BuildAndMap();

            RouteEndpoint endpoint = FindSingleEndpoint(app, ProviderAccountPattern, HttpMethods.Delete);
            IAuthorizeData[] authorize = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().ToArray();

            Assert.Contains(authorize, a => a.Policy == VideoForensicsPolicies.SuperAdminLocal);
        }

        private static IEndpointRouteBuilder BuildAndMap()
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            _ = builder.Host.UseDefaultServiceProvider(options =>
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
            _ = builder.Services.AddScoped(_ => Mock.Of<Common.Contracts.IAccountExportService>());
            _ = builder.Services.AddScoped(_ => Mock.Of<IRingAccountSyncState>());

            WebApplication app = builder.Build();
            app.MapAccountEndpoints();
            return app;
        }

        private static RouteEndpoint[] FindEndpoints(IEndpointRouteBuilder app, string pattern)
        {
            return app.DataSources
                .SelectMany(d => d.Endpoints)
                .OfType<RouteEndpoint>()
                .Where(e => StripConstraints(e.RoutePattern.RawText) == pattern)
                .ToArray();
        }

        private static RouteEndpoint FindSingleEndpoint(IEndpointRouteBuilder app, string pattern)
        {
            return FindSingleEndpoint(app, pattern, method: null);
        }

        /// <summary>Finds the one endpoint for a pattern and HTTP method, for patterns shared by GET and PUT (or POST).</summary>
        private static RouteEndpoint FindSingleEndpoint(IEndpointRouteBuilder app, string pattern, string? method)
        {
            RouteEndpoint[] matches = FindEndpoints(app, pattern)
                .Where(e => method is null || e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(method) == true)
                .ToArray();
            _ = Assert.Single(matches);
            return matches[0];
        }

        /// <summary>Removes route constraints so "{id:guid}" compares equal to "{id}".</summary>
        private static string? StripConstraints(string? rawText)
        {
            return rawText is null ? null : System.Text.RegularExpressions.Regex.Replace(rawText, @"\{(\w+):[^}]+\}", "{$1}");
        }
    }
}
