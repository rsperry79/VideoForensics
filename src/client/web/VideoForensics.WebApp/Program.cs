using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;

using Syncfusion.Blazor;

using System.Threading.RateLimiting;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Ui.Shared.Services;
using VideoForensics.WebApp.Api;
using VideoForensics.WebApp.Auth;
using VideoForensics.WebApp.Components;
using VideoForensics.WebApp.Discovery;
using VideoForensics.WebApp.Hubs;

// Which interfaces Kestrel binds to must be decided NOW, before the host is built - a listen
// socket can't be rebound live, so the network-tier setting can't wait for the normal DI/config
// pipeline (which needs the host built first) the way every other setting in this app does. This
// reads the ONE setting needed for that decision directly from the SQLite file via a lightweight
// ADO.NET connection - not the full EF/DI stack - and tolerates a missing file/table (first run,
// or a fresh install) by defaulting to Local, the safest "hasn't been configured yet" state (plan
// §5.2's "Local-only by default").
NetworkTier configuredNetworkTier = ReadConfiguredNetworkTierBeforeHostBuilds();

var syncfusionLicenseKeyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "VideoForensics", "syncfusion-license.key");
if (File.Exists(syncfusionLicenseKeyPath))
{
    Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(File.ReadAllText(syncfusionLicenseKeyPath).Trim());
}

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

var listenPort = ResolveConfiguredPort(builder.Configuration);
builder.WebHost.ConfigureKestrel(options =>
{
    if (configuredNetworkTier == NetworkTier.Local)
    {
        // Loopback only - genuinely unreachable from the LAN or internet at the socket level, not
        // merely discouraged by application logic. A device on the same network cannot even open a
        // TCP connection, let alone attempt to pair.
        options.ListenLocalhost(listenPort);
    }
    else
    {
        // Network and Internet both listen on every interface - the plan's own §5.3 note on the
        // Cloudflare Tunnel already establishes that "Internet" exposure comes from cloudflared
        // forwarding to localhost, not from Kestrel itself binding to a public address, so there is
        // no separate wider bind for Internet beyond what Network already needs for LAN reachability.
        options.ListenAnyIP(listenPort);
    }
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options => options.DetailedErrors = builder.Environment.IsDevelopment());

builder.Services.AddSyncfusionBlazor();

// WebAuthn/passkey pairing (plan §5.1/M6). ServerDomain/Origins are dev defaults for the
// local/LAN case (§5.2's Local and Network tiers, no tunnel) - the Internet tier (Cloudflare
// Tunnel, M6's later network-tier work) will need this to reflect the tunnel's public origin
// instead, which is a configuration concern for that work, not this registration.
builder.Services.AddFido2(options =>
{
    options.ServerDomain = "localhost";
    options.ServerName = "VideoForensics";
    options.Origins = new HashSet<string> { "https://localhost:5162", "http://localhost:5162" };
});

builder.Services.AddAuthentication(PairedDeviceAuthenticationDefaults.SchemeName)
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, PairedDeviceAuthenticationHandler>(
        PairedDeviceAuthenticationDefaults.SchemeName, _ => { });

builder.Services.AddAuthorization(options => options.AddVideoForensicsPolicies());

// AddPolicy() above only registers the requirement TYPES a policy needs satisfied - it does not
// register the handler CLASSES that actually satisfy them. Without these, ASP.NET Core's
// authorization system finds zero IAuthorizationHandler for MinimumRoleRequirement/
// RequireLocalTierRequirement, so context.Succeed() is never called for ANY role or tier check and
// every one of these policies (ReadOnly/Review/Admin/SuperAdmin/SuperAdminLocal - i.e. every
// RBAC-gated endpoint in the app) fails unconditionally, regardless of the caller's actual role or
// network tier. Found by live-testing an authenticated SuperAdmin-over-loopback request that still
// 403'd despite both individual checks being correct by inspection.
builder.Services.AddSingleton<IAuthorizationHandler, MinimumRoleHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, RequireLocalTierHandler>();

// Session tokens, step-up tokens, and the SMTP password (SessionTokenService, StepUpAuthService,
// SmtpPasswordStore) are all IDataProtector-protected. AddDataProtection() alone relies on ASP.NET
// Core's default key-storage heuristic (usually %LOCALAPPDATA%\ASP.NET\DataProtection-Keys on
// Windows, keyed by content-root path) - explicit here so a signed-in session actually survives a
// server restart instead of depending on that heuristic continuing to resolve the same way. Keys
// live next to the app's own database rather than the OS default location, matching how every
// other piece of this app's persistent state is already rooted at %ProgramData%\VideoForensics.
var dataProtectionKeyPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "VideoForensics", "keys");
Directory.CreateDirectory(dataProtectionKeyPath);
builder.Services.AddDataProtection()
    .SetApplicationName("VideoForensics")
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyPath));

// One real-time channel for live download progress + urgent-event push (plan §6), for remote
// paired clients (MAUI) - the WebApp's own UI doesn't consume this hub at all (see LiveHub's doc
// comment). ILiveConnectionTracker is a singleton so DeviceManagementEndpoints' revoke handler can
// forcibly disconnect an already-open connection (plan §5.4), not just invalidate its token.
builder.Services.AddSignalR();
builder.Services.AddSingleton<ILiveConnectionTracker, LiveConnectionTracker>();
builder.Services.AddHostedService<DownloadProgressBroadcastService>();
builder.Services.AddScoped<INotificationProvider, SignalRNotificationProvider>();

// Rate limiting on auth/pairing endpoints (plan §5.7): keyed by the SAME network-tier-aware client
// IP resolution used everywhere else (INetworkTierResolver.ResolveClientIp, registered by
// AddVideoForensicsServerCore() below), so a request over the Cloudflare Tunnel is bucketed by the
// real client behind it, not Cloudflare's shared edge IP - the escalation-flagged mistake the plan
// calls out explicitly. A separate, more generous policy covers /api/media/* so an already-paired
// device can't hammer it into a self-inflicted DoS.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    _ = options.AddPolicy("auth", httpContext =>
    {
        INetworkTierResolver resolver = httpContext.RequestServices.GetRequiredService<INetworkTierResolver>();

        // This limiter exists to stop a REMOTE attacker from brute-forcing pairing/auth (plan
        // §5.7) - it was never meant to throttle the physically-present owner setting up their own
        // server over loopback, which is exactly what it was doing (5 requests/15min shared across
        // every pairing+auth call adds up fast across an initial pairing ceremony plus a sign-in:
        // initiate, register/options, register/complete, then assertion-options, assertion-complete
        // is already 5 on its own). Local-tier traffic is unlimited; Network/Internet keep the real
        // limit, since THAT'S the traffic this defense is actually for.
        if (resolver.ResolveTier(httpContext) == NetworkTier.Local)
        {
            return RateLimitPartition.GetNoLimiter(resolver.ResolveClientIp(httpContext));
        }

        var key = resolver.ResolveClientIp(httpContext);
        return RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(15),
            SegmentsPerWindow = 3,
            QueueLimit = 0
        });
    });

    _ = options.AddPolicy("media", httpContext =>
    {
        INetworkTierResolver resolver = httpContext.RequestServices.GetRequiredService<INetworkTierResolver>();
        var key = resolver.ResolveClientIp(httpContext);
        return RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 120,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 4,
            QueueLimit = 0
        });
    });

    // MCP HTTP endpoint rate limiting (Milestone 8): separate, more permissive policy than auth
    // since authenticated paired devices can issue expensive analytical queries in tight loops
    _ = options.AddPolicy("mcp", httpContext =>
    {
        INetworkTierResolver resolver = httpContext.RequestServices.GetRequiredService<INetworkTierResolver>();
        var key = resolver.ResolveClientIp(httpContext);
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
// active provider's four services, download/evidence orchestrators, JammingToolsOrchestrator) - see
// VideoForensics.Hosting/VideoForensicsHostingExtensions.cs. This is a deliberate, temporary
// bootstrap shape for this milestone: VideoForensics.WebApp owns its own local SQLite DB and talks
// to the active provider directly, same as console/MCP today. The long-term plan has this host as
// "the server" behind a future client/server API split that MAUI will consume instead - that split
// is separately scoped, later work.
// The active provider is read from IConfiguration's "ActiveProvider" setting (from appsettings.json
// or the VIDEOFORENSICS_ActiveProvider environment variable), defaulting to "Ring" for backward compatibility.
builder.Services.AddVideoForensicsDataLayer();
builder.Services.AddVideoForensicsServerCore(builder.Configuration["ActiveProvider"] ?? "Ring");

// Forensics query repositories (Phases 1-4) - MCP tools (Milestone 8 HTTP hosting)
_ = builder.Services.AddScoped<VideoForensics.Data.Common.Contracts.ITimelineRepository, VideoForensics.Data.Database.Repositories.TimelineRepository>();
_ = builder.Services.AddScoped<VideoForensics.Data.Common.Contracts.IIntegrityRepository, VideoForensics.Data.Database.Repositories.IntegrityRepository>();
_ = builder.Services.AddScoped<VideoForensics.Data.Common.Contracts.ICorrelationRepository, VideoForensics.Data.Database.Repositories.CorrelationRepository>();
_ = builder.Services.AddScoped<VideoForensics.Data.Common.Contracts.IAuditTrailRepository, VideoForensics.Data.Database.Repositories.AuditTrailRepository>();

// MCP Tool classes (Phases 1-4) - Milestone 8 HTTP hosting
_ = builder.Services.AddScoped<VideoForensics.WebApp.Mcp.Tools.TimelineTools>();
_ = builder.Services.AddScoped<VideoForensics.WebApp.Mcp.Tools.IntegrityTools>();
_ = builder.Services.AddScoped<VideoForensics.WebApp.Mcp.Tools.CorrelationTools>();
_ = builder.Services.AddScoped<VideoForensics.WebApp.Mcp.Tools.AuditTrailTools>();
_ = builder.Services.AddScoped<VideoForensics.WebApp.Mcp.Tools.JammingTools>();

// MCP Server: HTTP transport, attribute-discovered tools (Milestone 8)
_ = builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

// WebApp-specific service for handling 2FA authentication attempts: stores credentials in memory
// during the two-factor flow, with automatic 5-minute expiry.
builder.Services.AddSingleton<VideoForensics.WebApp.Api.IAuthAttemptCache, VideoForensics.WebApp.Api.AuthAttemptCache>();

// Bulk validation service for running full validation across all devices.
builder.Services.AddScoped<VideoForensics.WebApp.Services.BulkValidationService>();

builder.Services.AddHealthChecks();

// LAN discovery (plan §5.2) - advertises _videoforensics._tcp.local so a pairing client can find
// this server's address without the owner typing an IP. WebApp-only: console/MCP have no pairing
// API to advertise, unlike DeviceHealthSyncService which genuinely does run on every server-tier
// host.
builder.Services.AddHostedService<MdnsAdvertisementService>();

// Cloudflare Tunnel management for the Remote Access screen (plan §5.3) - a singleton since it
// wraps at most one managed cloudflared child process for the whole app, not a per-request or
// per-circuit concern.
builder.Services.AddSingleton<ICloudflaredTunnelService, CloudflaredTunnelService>();

// Client-side WebAuthn ceremony driver + circuit-scoped paired-device session (plan §5.1/§5.11) -
// the Blazor pages under Pages/Security*.razor and Pair.razor/DeviceSignIn.razor use these to talk
// to the pairing/auth API in Api/PairingEndpoints.cs.
builder.Services.AddScoped<PairedSessionState>();
builder.Services.AddScoped<WebAuthnClient>();

// Docked-layout chrome state (plan: docked MAUI/Blazor layout) - collapse state (device-local),
// right-panel page-context slot, and per-operator theme/culture, all circuit-scoped like
// PairedSessionState above.
builder.Services.AddScoped<LayoutPreferencesState>();
builder.Services.AddScoped<RightPanelContentService>();
builder.Services.AddScoped<ThemePreferenceService>();
builder.Services.AddSingleton<ICultureSwitcher, CultureSwitcher>();
builder.Services.AddLocalization();

// MainLayout.razor's shared <RadzenComponents> needs @rendermode="InteractiveServer" here - this
// is a real ASP.NET Core host with interactive server components configured below. MAUI's
// BlazorWebView registers NullBlazorRenderModeProvider instead - see IBlazorRenderModeProvider.
builder.Services.AddSingleton<IBlazorRenderModeProvider, InteractiveServerBlazorRenderModeProvider>();

// Native-dialog equivalents for the backup-export/import feature (Import/Export page): a browser can't
// show a real OS save dialog, so export hands off to a normal browser download instead (see
// WebFileDialogService/ExportDownloadTokenStore); folder selection for import's "media root" goes through
// a shared in-app directory browser instead (see IDirectoryBrowserService), since it works identically on
// both hosts (both run on the same local machine as the user in this app's deployment model).
builder.Services.AddSingleton<VideoForensics.WebApp.Services.ExportDownloadTokenStore>();
builder.Services.AddScoped<IFileDialogService, VideoForensics.WebApp.Services.WebFileDialogService>();
builder.Services.AddSingleton<IDirectoryBrowserService, DirectoryBrowserService>();

WebApplication app = builder.Build();

// DB init + Events backfill + persisted-config load, in that order - see
// VideoForensicsHostingExtensions.InitializeVideoForensicsDataAsync. Unlike the MCP server, a Web
// app has no "must respond immediately" constraint, so this is awaited directly before app.Run().
// A transient DB issue is logged critically but does not crash the whole web server - matching this
// project's existing philosophy of graceful degradation over hard crashes where reasonable.
ILogger<Program> initLogger = app.Services.GetRequiredService<ILogger<Program>>();
try
{
    await VideoForensicsHostingExtensions.InitializeVideoForensicsDataAsync(app.Services, initLogger, CancellationToken.None);
    initLogger.LogInformation("Database initialization (DB, Events backfill, config) completed.");
}
catch (Exception ex)
{
    initLogger.LogCritical(ex, "Database initialization failed. Continuing startup in a degraded state.");
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    _ = app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    _ = app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.UseAntiforgery();

app.MapStaticAssets();

app.MapVideoForensicsHealthEndpoints();

// Minimal API surface for paired clients (MAUI today; more later) - see Api/MediaApiEndpoints.cs
// for the explicit "unauthenticated until M6" note.
app.MapMediaApiEndpoints();
app.MapReportEndpoints();
app.MapAuthEndpoints();
app.MapPairingEndpoints();
app.MapDeviceManagementEndpoints();
app.MapSecurityAuditLogEndpoints();
app.MapRemoteAccessEndpoints();
app.MapNotificationEndpoints();
app.MapEvidenceEndpoints();
app.MapNetworkSettingsEndpoints();
app.MapExportDownloadEndpoints();
app.MapBackupEndpoints();
app.MapDeviceConfigEndpoints();
app.MapEventEndpoints();
app.MapDownloadEndpoints();
app.MapAccountEndpoints();
app.MapConfigEndpoints();
app.MapDiscoveryEndpoints();

// MCP (Model Context Protocol) HTTP endpoint for forensic analysis tools (Milestone 8)
// Gated with paired-device authorization (matching other API endpoints)
_ = app.MapMcp("/mcp")
    .RequireAuthorization()
    .RequireRateLimiting("mcp");

app.MapHub<LiveHub>("/hubs/live");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    // Routable pages (Dashboard.razor, SignIn.razor, Accounts.razor) live in the shared RCL
    // (VideoForensics.Ui.Shared), a different assembly than App itself - without this,
    // MapRazorComponents only discovers routes in App's own assembly and every RCL page 404s
    // at the ASP.NET Core routing level (confirmed by actually running the app: GET / returned
    // a real HTTP 404, not a rendered Blazor "not found" page, which is what tipped this off).
    .AddAdditionalAssemblies(typeof(VideoForensics.Ui.Shared.Routes).Assembly);

app.Run();

static NetworkTier ReadConfiguredNetworkTierBeforeHostBuilds()
{
    try
    {
        var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "VideoForensics", "videoforensics.db");
        if (!File.Exists(dbPath))
        {
            return NetworkTier.Local;
        }

        using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM AppSettings WHERE Key = 'ConfiguredNetworkTier' LIMIT 1";
        var value = command.ExecuteScalar() as string;
        return Enum.TryParse<NetworkTier>(value, out NetworkTier tier) ? tier : NetworkTier.Local;
    }
    catch
    {
        // Any failure here (DB locked by another process, table not created yet, corrupt row) falls
        // back to the safest default rather than risking an unintended wide-open bind.
        return NetworkTier.Local;
    }
}

static int ResolveConfiguredPort(IConfiguration configuration)
{
    var urls = configuration["ASPNETCORE_URLS"] ?? configuration["urls"];
    var first = urls?.Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
    if (first is not null && Uri.TryCreate(first, UriKind.Absolute, out Uri? uri))
    {
        return uri.Port;
    }

    return 5162; // Matches Properties/launchSettings.json's applicationUrl.
}
