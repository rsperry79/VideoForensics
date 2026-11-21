using CloudflaredKit;
using Fido2NetLib;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Syncfusion.Blazor;
using System.Security.Cryptography.X509Certificates;
using System.Threading.RateLimiting;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting;
using VideoForensics.Client.Hosting.Contracts;
using VideoForensics.Client.Hosting.Remote;
using VideoForensics.Client.Hosting.Services;
using VideoForensics.Client.Platform.Windows;
using VideoForensics.Client.Ui.Shared;
using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Client.Ui.Shared.Formatting;
using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Client.Ui.Shared.Services.Cases;
using VideoForensics.Client.Ui.Shared.Services.Inspector;
using VideoForensics.Client.Ui.Shared.Services.QueryApi;
using VideoForensics.Client.Ui.Shared.Services.Scope;
using VideoForensics.Client.WebApp.Api;
using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Client.WebApp.Chat;
using VideoForensics.Client.WebApp.Components;
using VideoForensics.Client.WebApp.Contracts;
using VideoForensics.Client.WebApp.Discovery;
using VideoForensics.Client.WebApp.Hubs;
using VideoForensics.Client.WebApp.Infrastructure;
using VideoForensics.Client.WebApp.Mcp;
using VideoForensics.Client.WebApp.Mcp.Tools;
using VideoForensics.Client.WebApp.RateLimiting;
using VideoForensics.Client.WebApp.Services;
using VideoForensics.Client.WebApp.Services.CircuitGuards;
using VideoForensics.Core.Helpers.Platform;
using SperryDev.Logger.DependencyInjection;
using SperryDev.Logger.Services;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Data.Core.Services;
using VideoForensics.Data.Database.Repositories;

namespace VideoForensics.Client.WebApp.Startup
{
    /// <summary>Rate-limit policies for the auth, media and MCP endpoints, moved out of Program.cs. Same policies.</summary>
    internal static class WebAppRateLimitRegistration
    {
        /// <summary>Adds the "auth", "media" and "mcp" rate-limit policies.</summary>
        internal static IServiceCollection AddWebAppRateLimiting(this IServiceCollection services)
        {
        // Rate limiting on auth/pairing endpoints (plan §5.7): keyed by the SAME network-tier-aware client
        // IP resolution used everywhere else (INetworkTierResolver.ResolveClientIp, registered by
        // AddVideoForensicsServerCore() below), so a request over the Cloudflare Tunnel is bucketed by the
        // real client behind it, not Cloudflare's shared edge IP - the escalation-flagged mistake the plan
        // calls out explicitly. A separate, more generous policy covers /api/v1/media/* so an already-paired
        // device can't hammer it into a self-inflicted DoS.
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            _ = options.AddPolicy("auth", httpContext =>
            {
                INetworkTierResolver resolver = httpContext.RequestServices.GetRequiredService<INetworkTierResolver>();
                return AuthRateLimitPolicy.Create(httpContext, resolver);
            });

            _ = options.AddPolicy("media", httpContext =>
            {
                INetworkTierResolver resolver = httpContext.RequestServices.GetRequiredService<INetworkTierResolver>();
                return MediaRateLimitPolicy.Create(httpContext, resolver);
            });

            // MCP HTTP endpoint rate limiting (Milestone 8): separate, more permissive policy than auth
            // since authenticated paired devices can issue expensive analytical queries in tight loops
            _ = options.AddPolicy("mcp", httpContext =>
            {
                INetworkTierResolver resolver = httpContext.RequestServices.GetRequiredService<INetworkTierResolver>();
                string key = resolver.ResolveClientIp(httpContext);
                return RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 1000,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 10,
                    QueueLimit = 0
                });
            });
        });

        // Shared data layer + server-tier provider/orchestrator registrations (session provider,
            return services;
        }
    }
}
