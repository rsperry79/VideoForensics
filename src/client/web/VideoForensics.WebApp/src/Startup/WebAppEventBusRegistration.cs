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
    /// <summary>Event-bus and notice registrations moved out of Program.cs. Same order.</summary>
    internal static class WebAppEventBusRegistration
    {
        /// <summary>Adds the in-process change buses and their notifiers, sources and feeds.</summary>
        internal static IServiceCollection AddWebAppEventBuses(this IServiceCollection services)
        {
        // Thumbnail-ready announcements reach open evidence views: the live hub for remote clients, the bus for this process's
        // own circuits. Replaces the no-op default that AddVideoForensicsServerCore registers.
        services.AddSingleton<ThumbnailReadyBus>();
        services.AddSingleton<EvidenceChangedBus>();
        services.AddSingleton<DeviceHealthChangedBus>();
        services.AddSingleton<ClientConfigChangedBus>();
        services.AddSingleton<IConfigurationChangeNotifier, LiveConfigNotifier>();
        services.AddScoped<IConfigChangedSource, LocalConfigChangedSource>();
        services.Replace(ServiceDescriptor.Singleton<IMediaThumbnailNotifier, LiveThumbnailNotifier>());
        services.Replace(ServiceDescriptor.Singleton<IEvidenceChangeNotifier, LiveEvidenceNotifier>());
        services.Replace(ServiceDescriptor.Singleton<IDeviceHealthChangeNotifier, LiveDeviceHealthNotifier>());
        services.AddScoped<IThumbnailReadySource, LocalThumbnailReadySource>();
        services.AddScoped<IEvidenceChangedSource, LocalEvidenceChangedSource>();
        services.AddScoped<IDeviceHealthChangedSource, LocalDeviceHealthChangedSource>();
        // Media-flag review changes: the flag endpoints publish, open review pages subscribe (in-process and over LiveHub).
        services.AddMediaFlagChangeServices();
        // The dispatcher runs in this process, so each raised notice is published to open Notices pages directly.
        services.AddSingleton<LocalNoticeFeed>();
        services.AddSingleton<INoticeFeed>(sp => sp.GetRequiredService<LocalNoticeFeed>());
        // Account-linked announcements: the add-account wizard publishes, the accounts list subscribes. Scoped so one
        // circuit's link never reaches another circuit; both interfaces share one bus instance.
        services.AddScoped<AccountLinkedBus>();
        services.AddScoped<IAccountLinkedSource>(sp => sp.GetRequiredService<AccountLinkedBus>());
        services.AddScoped<IAccountLinkedNotifier>(sp => sp.GetRequiredService<AccountLinkedBus>());
            return services;
        }
    }
}
