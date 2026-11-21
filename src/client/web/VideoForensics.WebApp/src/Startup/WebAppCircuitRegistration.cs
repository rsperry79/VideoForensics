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
    /// <summary>
    /// Registrations moved out of Program.cs, one method per area. Each keeps the order it had in Program.cs.
    /// </summary>
    internal static class WebAppCircuitRegistration
    {
        /// <summary>Per-circuit sources for live status, download progress and notifications.</summary>
        internal static IServiceCollection AddWebAppCircuitStatusSources(this IServiceCollection services)
        {
        // Per-circuit, so each circuit samples the sync activity log itself. Replaces the Razor panel's polling timer.
        services.AddScoped<IDownloadProgressSource, LocalDownloadProgressSource>();
        services.AddSingleton<VideoForensics.Client.Ui.Shared.Contracts.IMaintenanceStatusSource, VideoForensics.Client.WebApp.Services.LocalMaintenanceStatusSource>();
        // Per-circuit like the scoped IRingSelfTestService it samples. Replaces the Razor page's status polling timer.
        services.AddScoped<ISelfTestStatusSource, LocalSelfTestStatusSource>();
        // The LiveView page resolves this in both hosts. The WebApp has no live push channel, so it reports disconnected and the page polls.
        services.AddScoped<ILiveViewSessionSource, LocalLiveViewSessionSource>();
        services.AddSingleton<ISelfTestStatusChangeDetector, SelfTestStatusChangeDetector>();
        services.AddHostedService<SelfTestStatusBroadcastService>();
        services.AddScoped<INotificationProvider, SignalRNotificationProvider>();
        services.AddScoped<VapidKeyProvider>();
        services.AddScoped<INotificationProvider, WebPushNotificationProvider>();
            return services;
        }

        /// <summary>The circuit-scoped WebAuthn client, paired-device session and step-up service.</summary>
        internal static IServiceCollection AddWebAppPairedSession(this IServiceCollection services)
        {
        // Client-side WebAuthn ceremony driver + circuit-scoped paired-device session (plan §5.1/§5.11) -
        // the Blazor pages under Pages/Security*.razor and Pair.razor/DeviceSignIn.razor use these to talk
        // to the pairing/auth API in Api/PairingEndpoints.cs.
        services.AddScoped<PairedSessionState>();
        services.AddScoped<WebAuthnClient>();
        services.AddScoped<StepUpService>();
        services.AddScoped<IMediaContentUrlProvider, LocalMediaContentUrlProvider>();
            return services;
        }
    }
}
