using CloudflaredKit;

using Fido2NetLib;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

using Serilog;

using Syncfusion.Blazor;

using System.Security.Cryptography.X509Certificates;
using System.Threading.RateLimiting;

using VideoForensics.Data.Core.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Platform.Windows;
using VideoForensics.Client.WebApp.Api;
using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Client.WebApp.Chat;
using VideoForensics.Client.WebApp.Contracts;
using VideoForensics.Client.WebApp.Discovery;
using VideoForensics.Client.WebApp.Hubs;
using VideoForensics.Client.WebApp.Infrastructure;
using VideoForensics.Client.WebApp.Mcp;
using VideoForensics.Client.WebApp.Mcp.Tools;
using VideoForensics.Client.WebApp.RateLimiting;
using VideoForensics.Client.WebApp.Services;
using VideoForensics.Client.WebApp.Services.CircuitGuards;
using VideoForensics.Core.Helpers.Forensics.KeyManagement;
using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Core.Logging.DependencyInjection;
using VideoForensics.Core.Logging.Services;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Services;
using VideoForensics.Data.Database.Repositories;
using VideoForensics.Client.Hosting;
using VideoForensics.Client.Hosting.Contracts;
using VideoForensics.Client.Hosting.Remote;
using VideoForensics.Client.Hosting.Services;
using VideoForensics.Client.Ui.Shared;
using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Client.Ui.Shared.Formatting;
using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Client.Ui.Shared.Services.Cases;
using VideoForensics.Client.Ui.Shared.Services.Inspector;
using VideoForensics.Client.Ui.Shared.Services.QueryApi;
using VideoForensics.Client.Ui.Shared.Services.Scope;
using VideoForensics.Client.WebApp.Components;

// Windows host: read install-time storage overrides from the registry before any StorageLocationProvider is created.
VideoForensics.Core.Helpers.Platform.StorageOverrides.Current = new VideoForensics.Client.Platform.Windows.RegistryStorageOverrideSource();
VideoForensics.Core.Helpers.Platform.DataDirectoryAccess.Current = new VideoForensics.Client.Platform.Windows.WindowsDataDirectoryAccess();
VideoForensics.Core.Helpers.Forensics.KeyManagement.KeyStorageProviders.PlatformProviders = VideoForensics.Client.Platform.Windows.KeyManagement.WindowsKeyStorageProviders.Create;

// Which interfaces Kestrel binds to must be decided NOW, before the host is built - a listen
// socket can't be rebound live, so the network-tier setting can't wait for the normal DI/config
// pipeline (which needs the host built first) the way every other setting in this app does. This
// reads the ONE setting needed for that decision directly from the SQLite file via a lightweight
// ADO.NET connection - not the full EF/DI stack - and tolerates a missing file/table (first run,
// or a fresh install) by defaulting to Local, the safest "hasn't been configured yet" state (plan
// §5.2's "Local-only by default").
NetworkTier configuredNetworkTier = new NetworkTierConfigReader(new StorageLocationProvider()).ReadConfiguredTier();

// Configure Serilog for structured audit logging BEFORE creating the host.
// Dual-write: file sinks (primary/truth) + IActionLogRepository (secondary/searchable).
var storageProvider = new StorageLocationProvider();
string logsDir = storageProvider.GetDefaultRoot(StorageCategory.Logs);
Directory.CreateDirectory(logsDir);

Log.Logger = new LoggerConfiguration()
    // Every level is recorded. The Server Logs filters only change what is shown, so Trace is kept for later review.
    .MinimumLevel.Verbose()
    // EF SQL command messages are logged at Debug (see EfCommandLogLevelLoggerFactory); Serilog must let Debug through for them.
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Debug)
    // Ring API call trace (see the subscription after app.Build): Debug, so it is off unless the category is enabled.
    .MinimumLevel.Override("VideoForensics.Providers.Ring.Api", Serilog.Events.LogEventLevel.Debug)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "VideoForensics.Client.WebApp")
    // Text format: human-readable with structured properties appended
    .WriteTo.File(
        path: Path.Combine(logsDir, "audit-.txt"),
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        shared: true)
    // JSON format: structured output with all properties
    .WriteTo.File(
        path: Path.Combine(logsDir, "audit-.json"),
        outputTemplate: "{{\"Timestamp\":\"{Timestamp:o}\",\"Level\":\"{Level}\",\"Message\":\"{Message:lj}\",\"Exception\":\"{Exception}\",\"Properties\":{Properties:j}}}{NewLine}",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        shared: true)
    .CreateLogger();

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// In-place updates are off by default (Updates:Enabled). Apply a staged package before the host starts. Only as a real
// Windows service: a debug run from Visual Studio must never rewrite its own build output.
bool inPlaceUpdatesEnabled = OperatingSystem.IsWindows() && builder.Configuration.GetValue<bool>("Updates:Enabled");
string updatesRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "VideoForensics", "updates");
bool allowDevUpdatePackages = builder.Configuration.GetValue<bool>("Updates:AllowDevPackages");
bool updatesActiveAsService = inPlaceUpdatesEnabled && Microsoft.Extensions.Hosting.WindowsServices.WindowsServiceHelpers.IsWindowsService();
if (updatesActiveAsService)
{
    // Files renamed aside by the previous apply (the exe and dlls that were in use) can be deleted now.
    VideoForensics.Client.Hosting.Updates.UpdateApplier.DeleteLeftoverFiles(AppContext.BaseDirectory);

    // Only SYSTEM and Administrators may write the folder this service applies updates from as SYSTEM.
    // Dev mode adds the developer (Updates:DevWriterUser, set once by enable-dev-updates.ps1) so Visual Studio can stage without admin.
    var updateWriters = new List<System.Security.Principal.SecurityIdentifier>();
    string? devWriterUser = builder.Configuration["Updates:DevWriterUser"];
    if (allowDevUpdatePackages && !string.IsNullOrWhiteSpace(devWriterUser))
    {
        try
        {
            updateWriters.Add((System.Security.Principal.SecurityIdentifier)new System.Security.Principal.NTAccount(devWriterUser)
                .Translate(typeof(System.Security.Principal.SecurityIdentifier)));
        }
        catch (System.Security.Principal.IdentityNotMappedException)
        {
            Log.Error("Updates:DevWriterUser '{User}' is not a known account; only SYSTEM and Administrators can stage updates", devWriterUser);
        }
    }

    VideoForensics.Client.Platform.Windows.UpdatesFolderAcl.Ensure(updatesRoot, updateWriters);

    // The apply below ends with an exit that the service manager must turn into a restart. If that cannot be guaranteed,
    // leave the staged package alone: a stopped service is worse than an old version.
    VideoForensics.Client.Platform.Windows.ServiceRecoveryResult recovery = await new VideoForensics.Client.Platform.Windows.WindowsServiceRecoveryConfigurator(
        Microsoft.Extensions.Logging.Abstractions.NullLogger<VideoForensics.Client.Platform.Windows.WindowsServiceRecoveryConfigurator>.Instance)
        .EnsureAsync("VideoForensics", CancellationToken.None);
    if (!recovery.Success)
    {
        Log.Error("Service recovery is not configured ({Message}); staged updates will not be applied", recovery.Message);
    }

    var startupApplier = new VideoForensics.Client.Hosting.Updates.StartupUpdateApplier(
        new VideoForensics.Client.Hosting.Updates.UpdatePackageVerifier(),
        new VideoForensics.Client.Hosting.Updates.UpdateApplier(Microsoft.Extensions.Logging.Abstractions.NullLogger<VideoForensics.Client.Hosting.Updates.UpdateApplier>.Instance),
        Microsoft.Extensions.Logging.Abstractions.NullLogger<VideoForensics.Client.Hosting.Updates.StartupUpdateApplier>.Instance);
    string runningVersion = System.Diagnostics.FileVersionInfo.GetVersionInfo(Environment.ProcessPath!).ProductVersion ?? "0.0.0";
    VideoForensics.Client.Hosting.Updates.StartupUpdateResult updateResult = recovery.Success
        ? await startupApplier.TryApplyStagedAsync(updatesRoot, AppContext.BaseDirectory, runningVersion, allowDevUpdatePackages, CancellationToken.None)
        : new VideoForensics.Client.Hosting.Updates.StartupUpdateResult(VideoForensics.Client.Hosting.Updates.StartupUpdateOutcome.None, "recovery not configured", null);

    if (updateResult.Outcome == VideoForensics.Client.Hosting.Updates.StartupUpdateOutcome.Applied)
    {
        Log.Information("Applied staged update {Version}; exiting so the service manager restarts the new version", updateResult.Version);
        await Log.CloseAndFlushAsync();

        // This process still has the old files mapped, so it must not keep running. A non-zero exit triggers the
        // service's SCM recovery action (restart); the recovery settings were ensured above.
        Environment.Exit(1);
    }
    else if (updateResult.Outcome != VideoForensics.Client.Hosting.Updates.StartupUpdateOutcome.None)
    {
        Log.Error("Staged update {Outcome}: {Message}", updateResult.Outcome, updateResult.Message);
    }
}

// Must run before AddSyncfusionBlazor() below. Resolves the key from env vars and Client.Core user secrets
// itself, so it does not depend on builder.Configuration having been built.
VideoForensicsHostingExtensions.RegisterSyncfusionLicenseIfPresent();

// Windows Service hosting - enables running under Windows Service Control Manager while keeping
// dotnet run/debugging unchanged (UseWindowsService is a no-op on non-Windows platforms and when
// not running under the SCM, so no conditional is needed).
builder.Host.UseWindowsService(options => options.ServiceName = "VideoForensics");

// systemd integration on Linux - enables running under systemd while keeping dotnet run/debugging
// unchanged (UseSystemd is a no-op on non-Linux platforms and when not running under systemd).
builder.Host.UseSystemd();

IReadOnlyList<ListenEndpoint> listenEndpoints = ServerAddressResolver.ResolveListenEndpoints(builder.Configuration);

// The certificate Kestrel presents on https listeners. Filled by LetsEncryptRenewalService; empty until then,
// in which case Kestrel falls back to its default certificate.
var certificateHolder = new CertificateHolder();
builder.WebHost.ConfigureKestrel(options =>
{
    foreach (ListenEndpoint endpoint in listenEndpoints)
    {
        // https endpoints get TLS via the ASP.NET development certificate; http endpoints stay plain.
        void ConfigureHttps(ListenOptions listenOptions)
        {
            if (string.Compare(endpoint.Scheme, "https", StringComparison.Ordinal) == 0)
            {
                _ = listenOptions.UseHttps(https => https.ServerCertificateSelector = (_, _) => certificateHolder.Current);
            }
        }

        if (configuredNetworkTier == NetworkTier.Local)
        {
            // Loopback only - genuinely unreachable from the LAN or internet at the socket level, not
            // merely discouraged by application logic. A device on the same network cannot even open a
            // TCP connection, let alone attempt to pair.
            options.ListenLocalhost(endpoint.Port, ConfigureHttps);
        }
        else
        {
            // Network and Internet both listen on every interface - the plan's own §5.3 note on the
            // Cloudflare Tunnel already establishes that "Internet" exposure comes from cloudflared
            // forwarding to localhost, not from Kestrel itself binding to a public address, so there is
            // no separate wider bind for Internet beyond what Network already needs for LAN reachability.
            options.ListenAnyIP(endpoint.Port, ConfigureHttps);
        }
    }
});

// The endpoints above are the only addresses this server binds. Clearing the urls setting stops Kestrel from logging that it
// is overriding a launch-profile or ASPNETCORE_URLS address.
builder.WebHost.UseUrls([]);

// Serilog is already configured above with file sinks. Optionally enable additional sinks
// (Windows Event Log for service visibility, Linux syslog) via AddVideoForensicsLogging.
builder.Logging.SetMinimumLevel(LogLevel.Trace);
// The Server Logs history reads the daily audit files in logsDir; they hold every level recorded above.
builder.Services.AddSingleton(new LogFileDirectory(logsDir));
// Note: AddVideoForensicsLogging detects pre-configured Serilog and integrates it;
// the logFilePath parameter is kept for backward compat with other hosts that don't pre-configure Serilog.
// Single buffer instance: the logging provider writes to it and the /api/v1/logs endpoints read it.
builder.Services.AddInMemoryLogBuffer();
builder.Logging.AddVideoForensicsLogging(string.Empty, LogLevel.Trace, enableEventLog: true, enableSyslog: true,
    eventLogProvider: OperatingSystem.IsWindows() ? (Action<ILoggingBuilder>?)VideoForensics.Client.Platform.Windows.Logging.WindowsLoggingProviders.AddEventLog : null);

// Add services to the container.
// Browser time zone for displaying stored UTC times in the user's zone (see UserTimeZoneHost).
builder.Services.AddBlazorLocalTimeService();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options => options.DetailedErrors = builder.Environment.IsDevelopment());

builder.Services.AddHttpContextAccessor();

builder.Services.AddSyncfusionBlazor();

// WebAuthn/passkey pairing (plan §5.1/M6). ServerDomain/Origins are dev defaults for the
// local/LAN case (§5.2's Local and Network tiers, no tunnel) - the Internet tier (Cloudflare
// Tunnel, M6's later network-tier work) will need this to reflect the tunnel's public origin
// instead, which is a configuration concern for that work, not this registration.
builder.Services.AddFido2(options =>
{
    options.RPName = "VideoForensics";
    options.Origins = new HashSet<string>(ServerAddressResolver.ResolveAllowedOrigins(builder.Configuration), StringComparer.Ordinal);
});

// The RP ID follows the Internet address the operator configured, so passkeys registered through the
// tunnel match its host. This runs when the options are first read, after the saved configuration loads.
// Fido2 is built per scope from the saved Internet address. The options registered above are shared and
// their RP ID was never seen by the endpoints (they kept "localhost"), so the RP ID is set here directly.
builder.Services.AddScoped<IFido2>(sp =>
{
    IForensicsConfiguration forensics = sp.GetRequiredService<IForensicsConfiguration>();
    var origins = new HashSet<string>(ServerAddressResolver.ResolveAllowedOrigins(builder.Configuration), StringComparer.Ordinal);
    if (Uri.TryCreate(forensics.InternetServerUrl, UriKind.Absolute, out Uri? internetUrl))
    {
        _ = origins.Add(internetUrl.GetLeftPart(UriPartial.Authority));
    }

    return new Fido2(new Fido2Configuration
    {
        RPID = ServerAddressResolver.ResolveRpId(builder.Configuration, forensics.InternetServerUrl),
        RPName = "VideoForensics",
        Origins = origins,
    });
});

builder.Services.AddOptions<Fido2Configuration>()
    .Configure<IForensicsConfiguration>((options, forensics) =>
        options.RPID = ServerAddressResolver.ResolveRpId(builder.Configuration, forensics.InternetServerUrl));

builder.Services.AddAuthentication(PairedDeviceAuthenticationDefaults.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, PairedDeviceAuthenticationHandler>(
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
builder.Services.AddSingleton<IAuthorizationHandler, RequireLocalOrPasskeyHandler>();

// Read per request, so a change on the Permissions page applies without a restart.
builder.Services.AddScoped<IProviderAccountEditPolicy, ProviderAccountEditPolicy>();

// Session tokens, step-up tokens, and the SMTP password (SessionTokenService, StepUpAuthService,
// SmtpPasswordStore) are all IDataProtector-protected. AddDataProtection() alone relies on ASP.NET
// Core's default key-storage heuristic (usually %LOCALAPPDATA%\ASP.NET\DataProtection-Keys on
// Windows, keyed by content-root path) - explicit here so a signed-in session actually survives a
// server restart instead of depending on that heuristic continuing to resolve the same way. Keys
// live next to the app's own database rather than the OS default location, matching how every
// other piece of this app's persistent state is already rooted at %ProgramData%\VideoForensics.
string dataProtectionKeyPath = storageProvider.GetDefaultRoot(StorageCategory.Keys);
Directory.CreateDirectory(dataProtectionKeyPath);
IDataProtectionBuilder dataProtectionBuilder = builder.Services.AddDataProtection()
    .SetApplicationName("VideoForensics")
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyPath));
// DPAPI encrypts the key-ring file at rest but is Windows-only (this app is platform-agnostic -
// see PlatformDirectoryService/CredentialEncryptionFactory for the same Windows/other-OS split).
// The WebApp may run interactively during development and as LocalSystem in production. Use
// machine scope so a key ring created by one of those identities remains usable by the service.
// Access to the key directory is still restricted by its filesystem ACL.
// When the TPM is usable, the key ring is encrypted to a TPM-held certificate, so only this machine's TPM can
// decrypt it. Otherwise DPAPI (machine scope) protects it.
if (OperatingSystem.IsWindows())
{
    X509Certificate2? tpmCertificate = VideoForensics.Client.Platform.Windows.KeyManagement.TpmDataProtectionCertificate.TryLoadOrCreate();
    _ = tpmCertificate is not null
        ? dataProtectionBuilder.ProtectKeysWithCertificate(tpmCertificate)
        : dataProtectionBuilder.ProtectKeysWithDpapi(protectToLocalMachine: true);
}

// One real-time channel for live download progress + urgent-event push (plan §6), for remote
// paired clients (MAUI) - the WebApp's own UI doesn't consume this hub at all (see LiveHub's doc
// comment). ILiveConnectionTracker is a singleton so DeviceManagementEndpoints' revoke handler can
// forcibly disconnect an already-open connection (plan §5.4), not just invalidate its token.
builder.Services.AddSignalR();
builder.Services.AddSingleton<ILiveConnectionTracker, LiveConnectionTracker>();
// Live-view session pushes to subscribed clients (batch 3b). Replace, not Add: the Hosting registration
// later in this file (AddVideoForensicsServerCore) uses TryAddSingleton for the no-op publisher, so this
// registration must come first to win; Replace also guarantees a single descriptor for this interface.
builder.Services.Replace(ServiceDescriptor.Singleton<ILiveViewTelemetryPublisher, LiveViewTelemetryBroadcastService>());
builder.Services.AddSingleton<IDownloadProgressChangeDetector, DownloadProgressChangeDetector>();
builder.Services.Configure<BrowserLiveViewOptions>(
    builder.Configuration.GetSection(BrowserLiveViewOptions.SectionName));
builder.Services.AddSingleton<BrowserLiveViewBridge>();
builder.Services.AddHostedService<DownloadProgressBroadcastService>();
// Per-circuit, so each circuit samples the sync activity log itself. Replaces the Razor panel's polling timer.
builder.Services.AddScoped<IDownloadProgressSource, LocalDownloadProgressSource>();
// Per-circuit like the scoped IRingSelfTestService it samples. Replaces the Razor page's status polling timer.
builder.Services.AddScoped<ISelfTestStatusSource, LocalSelfTestStatusSource>();
// The LiveView page resolves this in both hosts. The WebApp has no live push channel, so it reports disconnected and the page polls.
builder.Services.AddScoped<ILiveViewSessionSource, LocalLiveViewSessionSource>();
builder.Services.AddSingleton<ISelfTestStatusChangeDetector, SelfTestStatusChangeDetector>();
builder.Services.AddHostedService<SelfTestStatusBroadcastService>();
builder.Services.AddScoped<INotificationProvider, SignalRNotificationProvider>();
builder.Services.AddScoped<VapidKeyProvider>();
builder.Services.AddScoped<INotificationProvider, WebPushNotificationProvider>();

// Rate limiting on auth/pairing endpoints (plan §5.7): keyed by the SAME network-tier-aware client
// IP resolution used everywhere else (INetworkTierResolver.ResolveClientIp, registered by
// AddVideoForensicsServerCore() below), so a request over the Cloudflare Tunnel is bucketed by the
// real client behind it, not Cloudflare's shared edge IP - the escalation-flagged mistake the plan
// calls out explicitly. A separate, more generous policy covers /api/v1/media/* so an already-paired
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

        string key = resolver.ResolveClientIp(httpContext);
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
// active provider's four services, download/evidence orchestrators, JammingToolsOrchestrator) - see
// VideoForensics.Hosting/VideoForensicsHostingExtensions.cs. This is a deliberate, temporary
// bootstrap shape for this milestone: VideoForensics.Client.WebApp owns its own local SQLite DB and talks
// to the active provider directly, same as console/MCP today. The long-term plan has this host as
// "the server" behind a future client/server API split that MAUI will consume instead - that split
// is separately scoped, later work.
// The active provider is read from IConfiguration's "ActiveProvider" setting (from appsettings.json
// or the VIDEOFORENSICS_ActiveProvider environment variable), defaulting to "Ring" for backward compatibility.
builder.Services.AddVideoForensicsDataLayer();
builder.Services.AddVideoForensicsServerCore(builder.Configuration["ActiveProvider"] ?? "Ring");

// Platform update installer. Windows downloads the release .exe and launches it with /passive; Linux downloads the
// .deb to a pending-update location for a manual dpkg install. AddVideoForensicsServerCore registers no default.
if (inPlaceUpdatesEnabled)
{
    // Stage the release zip; StartupUpdateApplier applies it on the next service start. No installer is launched.
    _ = builder.Services.AddHttpClient();
    _ = builder.Services.AddSingleton<VideoForensics.Client.Hosting.Updates.IUpdatePackageVerifier>(
        new VideoForensics.Client.Hosting.Updates.UpdatePackageVerifier());
    _ = builder.Services.AddSingleton<VideoForensics.Client.Hosting.Updates.IUpdateStager>(sp =>
        new VideoForensics.Client.Hosting.Updates.UpdateStager(
            updatesRoot,
            sp.GetRequiredService<VideoForensics.Client.Hosting.Updates.IUpdatePackageVerifier>(),
            allowDevUpdatePackages,
            () => System.Diagnostics.FileVersionInfo.GetVersionInfo(Environment.ProcessPath!).ProductVersion ?? "0.0.0",
            sp.GetRequiredService<ILogger<VideoForensics.Client.Hosting.Updates.UpdateStager>>()));
    _ = builder.Services.AddSingleton<IUpdateInstaller>(sp =>
        new VideoForensics.Client.Hosting.Updates.StagedUpdateInstaller(
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<VideoForensics.Client.Hosting.Updates.IUpdateStager>(),
            updatesRoot,
            sp.GetRequiredService<ILogger<VideoForensics.Client.Hosting.Updates.StagedUpdateInstaller>>()));
}
else if (OperatingSystem.IsWindows())
{
    _ = builder.Services.AddSingleton<IUpdateInstaller, WindowsUpdateInstaller>();
}

if (updatesActiveAsService)
{
    // A package staged while the service runs (the dev stage script, or a downloaded release) triggers drain-and-restart.
    // NoOpUpdateDrain is replaced by the scheduler's drain when that lands.
    builder.Services.TryAddSingleton<VideoForensics.Client.Hosting.Updates.IUpdateDrain, VideoForensics.Client.Hosting.Updates.NoOpUpdateDrain>();
    _ = builder.Services.AddHostedService(sp => new VideoForensics.Client.Hosting.Updates.StagedUpdateWatcher(
        updatesRoot,
        new VideoForensics.Client.Hosting.Updates.UpdatePackageVerifier(),
        sp.GetRequiredService<VideoForensics.Client.Hosting.Updates.IUpdateDrain>(),
        allowDevUpdatePackages,
        () => System.Diagnostics.FileVersionInfo.GetVersionInfo(Environment.ProcessPath!).ProductVersion ?? "0.0.0",
        code => { Log.CloseAndFlush(); Environment.Exit(code); },
        TimeSpan.FromSeconds(5),
        sp.GetRequiredService<ILogger<VideoForensics.Client.Hosting.Updates.StagedUpdateWatcher>>()));
}
else
{
    _ = builder.Services.AddSingleton<IUpdateInstaller, VideoForensics.Client.Platform.Web.LinuxUpdateInstaller>();
}
// Thumbnail-ready announcements reach open evidence views: the live hub for remote clients, the bus for this process's
// own circuits. Replaces the no-op default that AddVideoForensicsServerCore registers.
builder.Services.AddSingleton<ThumbnailReadyBus>();
builder.Services.AddSingleton<EvidenceChangedBus>();
builder.Services.AddSingleton<DeviceHealthChangedBus>();
builder.Services.Replace(ServiceDescriptor.Singleton<IMediaThumbnailNotifier, LiveThumbnailNotifier>());
builder.Services.Replace(ServiceDescriptor.Singleton<IEvidenceChangeNotifier, LiveEvidenceNotifier>());
builder.Services.Replace(ServiceDescriptor.Singleton<IDeviceHealthChangeNotifier, LiveDeviceHealthNotifier>());
builder.Services.AddScoped<IThumbnailReadySource, LocalThumbnailReadySource>();
builder.Services.AddScoped<IEvidenceChangedSource, LocalEvidenceChangedSource>();
builder.Services.AddScoped<IDeviceHealthChangedSource, LocalDeviceHealthChangedSource>();
// The dispatcher runs in this process, so each raised notice is published to open Notices pages directly.
builder.Services.AddSingleton<LocalNoticeFeed>();
builder.Services.AddSingleton<INoticeFeed>(sp => sp.GetRequiredService<LocalNoticeFeed>());
// Account-linked announcements: the add-account wizard publishes, the accounts list subscribes. Scoped so one
// circuit's link never reaches another circuit; both interfaces share one bus instance.
builder.Services.AddScoped<AccountLinkedBus>();
builder.Services.AddScoped<IAccountLinkedSource>(sp => sp.GetRequiredService<AccountLinkedBus>());
builder.Services.AddScoped<IAccountLinkedNotifier>(sp => sp.GetRequiredService<AccountLinkedBus>());
// Probe-only snapshot endpoints; registers nothing unless SnapshotProbe:Enabled and SnapshotProbe:Token are set.
builder.Services.AddSnapshotProbe(builder.Configuration);

// Scheduled per-account event sync (server tier only). Registers nothing unless ScheduledTasks:EnabledProviders is non-empty.
builder.Services.AddVideoForensicsScheduledSync(builder.Configuration);

// Server-side Ring snapshot capture per enabled device (on by default; SnapshotCapture:Enabled=false turns it off).
builder.Services.AddVideoForensicsSnapshotCapture(builder.Configuration);
builder.Services.AddVideoForensicsAccountExport();

// Media analysis settings (hemisphere for season buckets), from the MediaAnalysis configuration section.
builder.Services.Configure<MediaAnalysisOptions>(builder.Configuration.GetSection("MediaAnalysis"));

// Forensics query repositories (Phases 1-4) - MCP tools (Milestone 8 HTTP hosting)
_ = builder.Services.AddScoped<ITimelineRepository, TimelineRepository>();
_ = builder.Services.AddScoped<IIntegrityRepository, IntegrityRepository>();
_ = builder.Services.AddScoped<ICorrelationRepository, CorrelationRepository>();
_ = builder.Services.AddScoped<IAuditTrailRepository, AuditTrailRepository>();

// LLM API key store for the embedded MCP chat assistant - stores LLM provider config
// (API key, model, base URL) encrypted via the existing ICredentialEncryptionProvider abstraction.
_ = builder.Services.AddScoped<ILlmApiKeyStore, LlmApiKeyStore>();

// MCP Tool classes (Phases 0.5-4) - Milestone 8 HTTP hosting
_ = builder.Services.AddScoped<SecurityEventTools>();
_ = builder.Services.AddScoped<TimelineTools>();
_ = builder.Services.AddScoped<IntegrityTools>();
_ = builder.Services.AddScoped<CorrelationTools>();
_ = builder.Services.AddScoped<AuditTrailTools>();
_ = builder.Services.AddScoped<JammingTools>();

// MCP Server: HTTP transport, attribute-discovered tools (Milestone 8)
_ = builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

// WebApp-specific service for handling 2FA authentication attempts: stores credentials in memory
// during the two-factor flow, with automatic 5-minute expiry.
builder.Services.AddSingleton<IAuthAttemptCache, AuthAttemptCache>();

// Bulk validation service for running full validation across all devices.
builder.Services.AddScoped<BulkValidationService>();

// First sync: runner, then validate-all as the media step, flagged complete only on full success.
builder.Services.AddScoped<InitialSyncCoordinator>();
builder.Services.AddScoped<IInitialSyncCoordinator>(sp => sp.GetRequiredService<InitialSyncCoordinator>());
builder.Services.AddScoped<InitialSyncResumer>();
builder.Services.AddHostedService<InitialSyncResumeService>();
builder.Services.AddScoped<IInitialSyncDispatcher>(sp => sp.GetRequiredService<InitialSyncCoordinator>());

// Embedded MCP-backed chat assistant: server-side implementation of IChatService that drives
// an LLM tool-use loop against this app's own forensic analysis tools.
builder.Services.AddScoped<IChatService, ChatOrchestrator>();

// Geo-IP and threat-intelligence blocking services for login pipeline
// GeoIP lookup service using MaxMind GeoLite2 database (stored in the main data directory)
string geoLite2DbPath = Path.Combine(storageProvider.GetDefaultRoot(StorageCategory.Database), "GeoLite2-Country.mmdb");
builder.Services.AddScoped<IGeoIpLookupService>(_ => new MaxMindGeoIpLookupService(geoLite2DbPath));

// Threat intelligence blocklist service (null implementation for now; actual feed fetching is separate work)
builder.Services.AddScoped<IThreatIntelBlocklistService, NullThreatIntelBlocklistService>();

// Banned IP range matching service
builder.Services.AddScoped<IBannedIpMatchService, BannedIpMatchService>();
// Singleton: the per-IP lockout counters must outlive a single request. Resolves its scoped repository per ban write.
builder.Services.AddSingleton<IFailedLoginIpTracker, FailedLoginIpTracker>();

builder.Services.AddHealthChecks();

// LAN discovery (plan §5.2) - advertises _videoforensics._tcp.local so a pairing client can find
// this server's address without the owner typing an IP. WebApp-only: console/MCP have no pairing
// API to advertise, unlike DeviceHealthSyncService which genuinely does run on every server-tier
// host.
builder.Services.AddHostedService<MdnsAdvertisementService>();

// Cloudflare Tunnel management for the Remote Access screen (plan §5.3) - a singleton since it
// wraps at most one managed cloudflared child process for the whole app, not a per-request or
// per-circuit concern.
// The tunnel token is supplied at runtime by CloudflaredTunnelService (via the options cache) before
// StartAsync runs, so the empty default configuration here never starts a TryCloudflare quick tunnel.
builder.Services.AddTryCloudflare(_ => { });
builder.Services.AddSingleton<ICloudflaredTunnelService, CloudflaredTunnelService>();

// Trusted certificate for the Internet hostname, issued through Let's Encrypt DNS-01 with the stored
// Cloudflare token. Acme:UseStaging (default true) keeps issuance on the staging CA until it is switched off.
builder.Services.AddSingleton(certificateHolder);
// Plain-http LAN requests are redirected to the tunnel or to https; see LocalTransportMiddleware.
int? httpsPort = listenEndpoints.FirstOrDefault(endpoint => endpoint.Scheme == "https")?.Port;
builder.Services.AddSingleton(new LocalTransportOptions(httpsPort, () => certificateHolder.Current is not null));
builder.Services.AddSingleton<IAcmeCertificateIssuer, CertesAcmeCertificateIssuer>();
builder.Services.AddScoped<ILetsEncryptCertificateStore, LetsEncryptCertificateStore>();
builder.Services.AddScoped<LetsEncryptCertificateManager>();
builder.Services.AddHostedService<LetsEncryptRenewalService>();
builder.Services.AddHostedService<CloudflareTunnelAutostart>();

// Windows Firewall rule management (plan §5.2) - when the configured network tier is widened
// beyond Local at runtime via Settings > Network Access, this ensures the corresponding inbound
// firewall rule exists (or removes it if narrowing back). The installer only creates the rule at
// fresh-install time if "local network" was chosen then; this closes the gap where changing the
// tier later still leaves the socket listening but firewalled (the exact bug being fixed).
builder.Services.AddSingleton<IFirewallRuleManager>(sp =>
    OperatingSystem.IsWindows()
        ? new VideoForensics.Client.Platform.Windows.WindowsFirewallRuleManager(sp.GetRequiredService<ILogger<VideoForensics.Client.Platform.Windows.WindowsFirewallRuleManager>>())
        : new NullFirewallRuleManager());

// Client-side WebAuthn ceremony driver + circuit-scoped paired-device session (plan §5.1/§5.11) -
// the Blazor pages under Pages/Security*.razor and Pair.razor/DeviceSignIn.razor use these to talk
// to the pairing/auth API in Api/PairingEndpoints.cs.
builder.Services.AddScoped<PairedSessionState>();
builder.Services.AddScoped<WebAuthnClient>();
builder.Services.AddScoped<StepUpService>();
builder.Services.AddScoped<IMediaContentUrlProvider, LocalMediaContentUrlProvider>();

// This circuit's real network tier (plan §5.10/§5.12), captured once from the browser's actual
// initial connection - see Components/NetworkTierCapture.razor and SessionNetworkContext's own doc
// comment for why this can't simply be re-resolved later from a self-HTTP call's own connection.
builder.Services.AddScoped<SessionNetworkContext>();

// Security Events page + SuperAdmin operator picker (plan §5.5): the WebApp's own Blazor UI reuses
// the same HTTP-backed RemoteSecurityEventsService/RemoteAdminOperatorService that a MAUI client
// uses, calling back into this same process's own Minimal API instead of a remote server address -
// see AddSelfHttpService's doc comment for why (SuperAdmin+Local network-tier authorization needs a
// fresh, real HttpContext per call, which a Blazor circuit alone can't guarantee for UI-event-driven
// code). This keeps the endpoint's own authorization rule the single source of truth for both hosts.
builder.Services.AddSelfHttpService<ISecurityEventsService>(http => new RemoteSecurityEventsService(http));
builder.Services.AddSelfHttpService<ILogViewerService>(http => new RemoteLogViewerService(http));
builder.Services.AddSelfHttpService<IAdminOperatorService>(http => new RemoteAdminOperatorService(http));

// Pages/Security*.razor (SecurityLockoutPolicy/SecurityDevices/SecurityOperators/SecurityAuditLog)
// build their own self-HTTP clients through this factory instead of a raw `new HttpClient { ... }` -
// same reasoning as AddSelfHttpService above. Registered ahead of Ui.Shared's
// DefaultSelfApiHttpClientFactory TryAddScoped registration so this one wins.
builder.Services.AddScoped<ISelfApiHttpClientFactory, WebAppSelfApiHttpClientFactory>();
builder.Services.AddScoped<IProviderAccountEditAccess, ProviderAccountEditAccess>();

// Client-side Web Push API driver for push notification subscription management.
builder.Services.AddScoped<WebPushClient>();

// Docked-layout chrome state (plan: docked MAUI/Blazor layout) - collapse state (device-local),
// right-panel page-context slot, and per-operator theme/culture, all circuit-scoped like
// PairedSessionState above.
builder.Services.AddScoped<LayoutPreferencesState>();
builder.Services.AddScoped<RightPanelContentService>();
builder.Services.AddScoped<LeftPanelContentService>();
builder.Services.AddScoped<InspectorState>();
builder.Services.TryAddSingleton(TimeProvider.System);
builder.Services.AddScoped<ScopeState>();
builder.Services.AddScoped<VideoForensics.Client.Ui.Shared.Services.Review.ReviewPageState>();
builder.Services.AddScoped<VideoForensics.Client.Ui.Shared.Services.Review.ReviewTimelineModel>();
builder.Services.AddScoped<VideoForensics.Client.Ui.Shared.Contracts.IReviewClipLoader, VideoForensics.Client.Ui.Shared.Services.Review.ReviewClipLoader>();
builder.Services.AddScoped<VideoForensics.Client.Ui.Shared.Contracts.IReviewEventLoader, VideoForensics.Client.Ui.Shared.Services.Review.ReviewEventLoader>();
builder.Services.AddScoped<VideoForensics.Client.Ui.Shared.Contracts.IReviewDetailLoader, VideoForensics.Client.Ui.Shared.Services.Review.ReviewDetailLoader>();
builder.Services.AddScoped<VideoForensics.Client.Ui.Shared.Services.Review.ReviewUrlCache>();
builder.Services.AddScoped<VideoForensics.Client.Ui.Shared.Services.Signal.SignalDrillTrail>();
builder.Services.AddScoped<DeviceOwnershipService>();
builder.Services.AddScoped<VideoForensics.Client.Ui.Shared.Contracts.ISignalAnalysisService, VideoForensics.Client.Hosting.Signal.SignalAnalysisService>();
builder.Services.AddScoped<IQueryApiFilterState, QueryApiFilterState>();
builder.Services.AddScoped<CaseState>();
builder.Services.AddScoped<ThemePreferenceService>();
builder.Services.AddScoped<IUiModeService, UiModeService>();
builder.Services.AddScoped<SimpleHomeBuilder>(sp => new SimpleHomeBuilder(
    sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<IMediaContentUrlProvider>(),
    sp.GetRequiredService<IStringLocalizer<VideoForensics.Client.Ui.Shared.Resources.SharedResources>>()));
builder.Services.AddScoped<IViewportService, DefaultViewportService>();
builder.Services.AddSingleton<ICultureSwitcher, CultureSwitcher>();
builder.Services.AddLocalization();

// Hosts that support interactive render modes register InteractiveServerBlazorRenderModeProvider - this
// is a real ASP.NET Core host with interactive server components configured below. MAUI's
// BlazorWebView registers NullBlazorRenderModeProvider instead - see IBlazorRenderModeProvider.
builder.Services.AddSingleton<IBlazorRenderModeProvider, InteractiveServerBlazorRenderModeProvider>();

// Native-dialog equivalents for the backup-export/import feature (Import/Export page): a browser can't
// show a real OS save dialog, so export hands off to a normal browser download instead (see
// WebFileDialogService/ExportDownloadTokenStore); folder selection for import's "media root" goes through
// a shared in-app directory browser instead (see IDirectoryBrowserService), since it works identically on
// both hosts (both run on the same local machine as the user in this app's deployment model).
builder.Services.AddSingleton<ExportDownloadTokenStore>();
builder.Services.AddScoped<IFileDialogService, WebFileDialogService>();
builder.Services.AddSingleton<IBackupStagingLocator, VideoForensics.Client.Core.Services.UserDocumentsBackupStagingLocator>();
builder.Services.AddSingleton<IFileSystemRootProvider>(OperatingSystem.IsWindows() ? new WindowsFileSystemRootProvider() : new VideoForensics.Client.Core.Services.PosixFileSystemRootProvider());
builder.Services.AddSingleton<IDirectoryBrowserService, DirectoryBrowserService>();

// System version information provider (Phase C Week 1) - immutable per build, cached at singleton scope
builder.Services.AddSingleton<ISystemVersionProvider, SystemVersionService>();

// Server-side checks for privileged in-process calls made from the Blazor circuit. Registered after every service
// they wrap, so each decorator wraps the final registration.
builder.Services.AddCircuitOperatorGuards();
builder.Services.AddScoped<IMcpCallerAuthorizer, McpCallerAuthorizer>();

WebApplication app = builder.Build();

// Debug trace of every Ring API call: method, path and query, and status. Response bodies are not logged because
// they hold camera and account data; enable the "VideoForensics.Providers.Ring.Api" category at Debug to see the trace.
Microsoft.Extensions.Logging.ILogger ringApiLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("VideoForensics.Providers.Ring.Api");
VideoForensics.Providers.Ring.ApiRawLogger.OnRawResponse += call =>
    ringApiLogger.LogDebug(
        "Ring API call #{CallNumber} {Method} {Target} -> {StatusCode} ({BodyLength} chars)",
        call.Sequence,
        call.Method,
        Uri.TryCreate(call.Url, UriKind.Absolute, out Uri? target) ? target.PathAndQuery : call.Url,
        call.StatusCode,
        call.Body?.Length ?? 0);

app.UseMiddleware<LocalTransportMiddleware>();

// The saved Internet address can change after startup. Add the browser origin to Fido2's allow-list and
// keep the RP ID on the saved address for the current request.
app.Use(async (context, next) =>
{
    // Resolved from the request scope, not the root provider: the Fido2 instance the endpoints use reads its
    // options from this scope, so updating the root instance never reached it (rpId stayed "localhost").
    WebAuthnOriginPolicy.Apply(
        context,
        context.RequestServices.GetRequiredService<IOptions<Fido2Configuration>>().Value,
        context.RequestServices.GetRequiredService<IForensicsConfiguration>());
    await next();
});

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

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.UseAntiforgery();

app.MapStaticAssets();

app.MapVideoForensicsHealthEndpoints();

app.MapSystemVersionEndpoints();

// Minimal API surface for paired clients (MAUI today; more later) - see Api/MediaApiEndpoints.cs
// for the explicit "unauthenticated until M6" note.
app.MapMediaApiEndpoints();
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
app.MapSoftwarePathsEndpoints();
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
app.MapDeviceConfigEndpoints();
app.MapDeviceControlEndpoints();
app.MapEventEndpoints();
app.MapCaseEndpoints();
app.MapJammingEndpoints();
app.MapSelfTestEndpoints();
app.MapAccountEndpoints();
app.MapConfigEndpoints();
app.MapDiscoveryEndpoints();
app.MapStorageSettingsEndpoints();
app.MapUpdateCheckEndpoints();
app.MapPushEndpoints();
app.MapChatEndpoints();
app.MapLlmSettingsEndpoints();
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

app.Run();
