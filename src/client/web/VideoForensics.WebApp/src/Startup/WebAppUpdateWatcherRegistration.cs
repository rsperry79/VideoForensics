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
    /// <summary>Update registrations, moved out of Program.cs.</summary>
    internal static class WebAppUpdateWatcherRegistration
    {
        /// <summary>Starts the staged-update watcher when updates run as a Windows service, else registers the Linux installer.</summary>
        internal static IServiceCollection AddWebAppUpdateWatcher(this IServiceCollection services, bool updatesActiveAsService, string updatesRoot, bool allowDevUpdatePackages)
        {
        if (updatesActiveAsService)
        {
            // A package staged while the service runs (the dev stage script, or a downloaded release) is drained, applied in place,
            // and the service then exits so the service manager restarts it on the new version.
            // The drain holds back new work through the maintenance gate and waits for in-flight work to finish before applying.
            services.TryAddSingleton<VideoForensics.Client.Hosting.Updates.IUpdateDrain, VideoForensics.Client.Hosting.Updates.MaintenanceUpdateDrain>();
            _ = services.AddHostedService(sp => new VideoForensics.Client.Hosting.Updates.StagedUpdateWatcher(
                updatesRoot,
                new VideoForensics.Client.Hosting.Updates.UpdatePackageVerifier(new VideoForensics.Client.Hosting.Updates.Ed25519PackageSignatureVerifier()),
                sp.GetRequiredService<VideoForensics.Client.Hosting.Updates.IUpdateDrain>(),
                new VideoForensics.Client.Hosting.Updates.StartupUpdateApplier(
                    new VideoForensics.Client.Hosting.Updates.UpdatePackageVerifier(new VideoForensics.Client.Hosting.Updates.Ed25519PackageSignatureVerifier()),
                    new VideoForensics.Client.Hosting.Updates.UpdateApplier(sp.GetRequiredService<ILogger<VideoForensics.Client.Hosting.Updates.UpdateApplier>>()),
                    sp.GetRequiredService<ILogger<VideoForensics.Client.Hosting.Updates.StartupUpdateApplier>>()),
                AppContext.BaseDirectory,
                allowDevUpdatePackages,
                () => System.Diagnostics.FileVersionInfo.GetVersionInfo(Environment.ProcessPath!).ProductVersion ?? "0.0.0",
                code => { Log.CloseAndFlush(); Environment.Exit(code); },
                TimeSpan.FromSeconds(5),
                sp.GetRequiredService<ILogger<VideoForensics.Client.Hosting.Updates.StagedUpdateWatcher>>()));
        }
        else
        {
            _ = services.AddSingleton<IUpdateInstaller, VideoForensics.Client.Platform.Web.LinuxUpdateInstaller>();
        }
            return services;
        }
    }
}
