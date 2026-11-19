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
    internal static class WebAppUpdateInstallerRegistration
    {
        /// <summary>Chooses the update installer for this platform: the staged-zip installer when in-place updates are on, else the Windows or Linux installer.</summary>
        internal static IServiceCollection AddWebAppUpdateInstaller(this IServiceCollection services, bool inPlaceUpdatesEnabled, string updatesRoot, bool allowDevUpdatePackages)
        {
        // Platform update installer. Windows downloads the release .exe and launches it with /passive; Linux downloads the
        // .deb to a pending-update location for a manual dpkg install. AddVideoForensicsServerCore registers no default.
        if (inPlaceUpdatesEnabled)
        {
            // Stage the release zip; StartupUpdateApplier applies it on the next service start. No installer is launched.
            _ = services.AddHttpClient();
            _ = services.AddSingleton<VideoForensics.Client.Hosting.Updates.IUpdatePackageVerifier>(
                new VideoForensics.Client.Hosting.Updates.UpdatePackageVerifier(new VideoForensics.Client.Hosting.Updates.Ed25519PackageSignatureVerifier()));
            _ = services.AddSingleton<VideoForensics.Client.Hosting.Updates.IUpdateStager>(sp =>
                new VideoForensics.Client.Hosting.Updates.UpdateStager(
                    updatesRoot,
                    sp.GetRequiredService<VideoForensics.Client.Hosting.Updates.IUpdatePackageVerifier>(),
                    allowDevUpdatePackages,
                    () => System.Diagnostics.FileVersionInfo.GetVersionInfo(Environment.ProcessPath!).ProductVersion ?? "0.0.0",
                    sp.GetRequiredService<ILogger<VideoForensics.Client.Hosting.Updates.UpdateStager>>()));
            _ = services.AddSingleton<IUpdateInstaller>(sp =>
                new VideoForensics.Client.Hosting.Updates.StagedUpdateInstaller(
                    sp.GetRequiredService<IHttpClientFactory>(),
                    sp.GetRequiredService<VideoForensics.Client.Hosting.Updates.IUpdateStager>(),
                    updatesRoot,
                    sp.GetRequiredService<ILogger<VideoForensics.Client.Hosting.Updates.StagedUpdateInstaller>>()));
        }
        else if (OperatingSystem.IsWindows())
        {
            _ = services.AddSingleton<IUpdateInstaller, WindowsUpdateInstaller>();
        }
            return services;
        }
    }
}
