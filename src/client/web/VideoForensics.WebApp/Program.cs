using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Api;
using VideoForensics.WebApp.Auth;
using VideoForensics.WebApp.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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

// Shared data layer + server-tier provider/orchestrator registrations (session provider, Ring's
// four services, download/evidence orchestrators, JammingToolsOrchestrator) - see
// VideoForensics.Hosting/VideoForensicsHostingExtensions.cs. This is a deliberate, temporary
// bootstrap shape for this milestone: VideoForensics.WebApp owns its own local SQLite DB and talks
// to the Ring provider directly, same as console/MCP today. The long-term plan has this host as
// "the server" behind a future client/server API split that MAUI will consume instead - that split
// is separately scoped, later work.
builder.Services.AddVideoForensicsDataLayer();
builder.Services.AddVideoForensicsServerCore();

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

app.UseAntiforgery();

app.MapStaticAssets();

// Minimal API surface for paired clients (MAUI today; more later) - see Api/MediaApiEndpoints.cs
// for the explicit "unauthenticated until M6" note.
app.MapMediaApiEndpoints();
app.MapPairingEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    // Routable pages (Dashboard.razor, SignIn.razor, Accounts.razor) live in the shared RCL
    // (VideoForensics.Ui.Shared), a different assembly than App itself - without this,
    // MapRazorComponents only discovers routes in App's own assembly and every RCL page 404s
    // at the ASP.NET Core routing level (confirmed by actually running the app: GET / returned
    // a real HTTP 404, not a rendered Blazor "not found" page, which is what tipped this off).
    .AddAdditionalAssemblies(typeof(VideoForensics.Ui.Shared.Routes).Assembly);

app.Run();
