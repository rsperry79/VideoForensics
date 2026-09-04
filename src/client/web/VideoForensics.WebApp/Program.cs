using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using VideoForensics.Hosting;
using VideoForensics.Ui.Shared.Services;
using VideoForensics.WebApp.Api;
using VideoForensics.WebApp.Auth;
using VideoForensics.WebApp.Components;
using VideoForensics.WebApp.Discovery;

var builder = WebApplication.CreateBuilder(args);

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

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    // Routable pages (Dashboard.razor, SignIn.razor, Accounts.razor) live in the shared RCL
    // (VideoForensics.Ui.Shared), a different assembly than App itself - without this,
    // MapRazorComponents only discovers routes in App's own assembly and every RCL page 404s
    // at the ASP.NET Core routing level (confirmed by actually running the app: GET / returned
    // a real HTTP 404, not a rendered Blazor "not found" page, which is what tipped this off).
    .AddAdditionalAssemblies(typeof(VideoForensics.Ui.Shared.Routes).Assembly);

app.Run();
