using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.Ui.Shared.Services;
using VideoForensics.WebApp.Api;
using VideoForensics.WebApp.Auth;
using VideoForensics.WebApp.Components;
using VideoForensics.WebApp.Discovery;
using VideoForensics.WebApp.Hubs;
using VideoForensics.Providers.Common.Contracts;

// Which interfaces Kestrel binds to must be decided NOW, before the host is built - a listen
// socket can't be rebound live, so the network-tier setting can't wait for the normal DI/config
// pipeline (which needs the host built first) the way every other setting in this app does. This
// reads the ONE setting needed for that decision directly from the SQLite file via a lightweight
// ADO.NET connection - not the full EF/DI stack - and tolerates a missing file/table (first run,
// or a fresh install) by defaulting to Local, the safest "hasn't been configured yet" state (plan
// §5.2's "Local-only by default").
var configuredNetworkTier = ReadConfiguredNetworkTierBeforeHostBuilds();

var builder = WebApplication.CreateBuilder(args);

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
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

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

    options.AddPolicy("auth", httpContext =>
    {
        var resolver = httpContext.RequestServices.GetRequiredService<INetworkTierResolver>();
        var key = resolver.ResolveClientIp(httpContext);
        return RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(15),
            SegmentsPerWindow = 3,
            QueueLimit = 0
        });
    });

    options.AddPolicy("media", httpContext =>
    {
        var resolver = httpContext.RequestServices.GetRequiredService<INetworkTierResolver>();
        var key = resolver.ResolveClientIp(httpContext);
        return RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 120,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 4,
            QueueLimit = 0
        });
    });
});

// Shared data layer + server-tier provider/orchestrator registrations (session provider, Ring's
// four services, download/evidence orchestrators, JammingToolsOrchestrator) - see
// VideoForensics.Hosting/VideoForensicsHostingExtensions.cs. This is a deliberate, temporary
// bootstrap shape for this milestone: VideoForensics.WebApp owns its own local SQLite DB and talks
// to the Ring provider directly, same as console/MCP today. The long-term plan has this host as
// "the server" behind a future client/server API split that MAUI will consume instead - that split
// is separately scoped, later work.
builder.Services.AddVideoForensicsDataLayer();
builder.Services.AddVideoForensicsServerCore();
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

var app = builder.Build();

// DB init + Events backfill + persisted-config load, in that order - see
// VideoForensicsHostingExtensions.InitializeVideoForensicsDataAsync. Unlike the MCP server, a Web
// app has no "must respond immediately" constraint, so this is awaited directly before app.Run().
// A transient DB issue is logged critically but does not crash the whole web server - matching this
// project's existing philosophy of graceful degradation over hard crashes where reasonable.
var initLogger = app.Services.GetRequiredService<ILogger<Program>>();
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
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
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
app.MapPairingEndpoints();
app.MapDeviceManagementEndpoints();
app.MapSecurityAuditLogEndpoints();
app.MapRemoteAccessEndpoints();
app.MapNotificationEndpoints();
app.MapNetworkSettingsEndpoints();
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
        var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VideoForensics", "videoforensics.db");
        if (!File.Exists(dbPath))
        {
            return NetworkTier.Local;
        }

        using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM AppSettings WHERE Key = 'ConfiguredNetworkTier' LIMIT 1";
        var value = command.ExecuteScalar() as string;
        return Enum.TryParse<NetworkTier>(value, out var tier) ? tier : NetworkTier.Local;
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
    if (first is not null && Uri.TryCreate(first, UriKind.Absolute, out var uri))
    {
        return uri.Port;
    }

    return 5162; // Matches Properties/launchSettings.json's applicationUrl.
}
