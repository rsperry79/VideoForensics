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
    /// <summary>Endpoint mapping moved out of Program.cs. Same order, same routes.</summary>
    internal static class WebAppEndpointMapping
    {
        /// <summary>Maps the WebApp's endpoints and Razor components onto the app, in the order Program.cs used.</summary>
        internal static WebApplication MapWebAppEndpoints(this WebApplication app)
        {
        app.MapStaticAssets();

        app.MapVideoForensicsHealthEndpoints();

        app.MapSystemVersionEndpoints();

        // Minimal API surface for paired clients (MAUI today; more later) - see Api/MediaApiEndpoints.cs
        // for the explicit "unauthenticated until M6" note.
        app.MapMediaApiEndpoints();
        app.MapMediaFlagEndpoints();
        app.MapSnapshotCaptureEndpoints();
        app.MapReportEndpoints();
        app.MapLiveViewEndpoints();
        app.MapAuthEndpoints();
        app.MapOperatorAuthEndpoints();
        app.MapSecurityEventsEndpoints();
        app.MapSetupEndpoints();
        app.MapPairingEndpoints();
        app.MapDeviceCodePairingEndpoints();
        app.MapDeviceManagementEndpoints();
        app.MapSecurityAuditLogEndpoints();
        app.MapRemoteAccessEndpoints();
        app.MapNotificationEndpoints();
        app.MapEvidenceEndpoints();
        app.MapNetworkSettingsEndpoints();
        app.MapReleaseChannelEndpoints();
        app.MapLogEndpoints();
        app.MapLockoutPolicyEndpoints();
        app.MapProviderPermissionEndpoints();
        app.MapBannedIpsEndpoints();
        app.MapTwoFactorPolicyEndpoints();
        app.MapAuthMethodEndpoints();
        app.MapDeviceCaptureModeEndpoints();
        app.MapExportDownloadEndpoints();
        app.MapBackupEndpoints();
        app.MapBackupUploadEndpoints();
        app.MapDeviceConfigEndpoints();
        app.MapDeviceControlEndpoints();
        app.MapEventEndpoints();
        app.MapCaseEndpoints();
        app.MapNoteEndpoints();
        app.MapJammingEndpoints();
        app.MapSelfTestEndpoints();
        app.MapAccountEndpoints();
        app.MapConfigEndpoints();
        app.MapDiscoveryEndpoints();
        app.MapStorageSettingsEndpoints();
        app.MapFolderBrowseEndpoints();
        app.MapUpdateCheckEndpoints();
        app.MapPushEndpoints();
        app.MapNotificationPreferencesEndpoints();
        app.MapNoticeEndpoints();
        app.MapChatEndpoints();
        app.MapLlmSettingsEndpoints();
        app.MapLogPipeEndpoints();
        app.MapDatabaseEndpoints();
        app.MapSnapshotProbeEndpoints();

        // MCP (Model Context Protocol) HTTP endpoint for forensic analysis tools (Milestone 8)
        // Gated with paired-device authorization (matching other API endpoints)
        _ = app.MapMcp("/mcp")
            .RequireAuthorization()
            .RequireRateLimiting("mcp");

        app.MapHub<LiveHub>("/hubs/live");
        app.MapHub<LiveViewSignalingHub>("/hubs/liveview-signaling");

        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            // Routable pages (Dashboard.razor, SignIn.razor, Accounts.razor) live in the shared RCL
            // (VideoForensics.Client.Ui.Shared), a different assembly than App itself - without this,
            // MapRazorComponents only discovers routes in App's own assembly and every RCL page 404s
            // at the ASP.NET Core routing level (confirmed by actually running the app: GET / returned
            // a real HTTP 404, not a rendered Blazor "not found" page, which is what tipped this off).
            .AddAdditionalAssemblies(typeof(Routes).Assembly);
            return app;
        }
    }
}
