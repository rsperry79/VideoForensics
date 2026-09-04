using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VideoForensics.Client.Common;
using VideoForensics.Client.Core;
using VideoForensics.Client.Core.Tools;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Data.Core.DependencyInjection;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.DependencyInjection;
using VideoForensics.Data.Database.Repositories;
using VideoForensics.Data.Database.Sqlite.DependencyInjection;
using VideoForensics.Data.Database.Sqlite.Migrations;
using VideoForensics.Hosting.BackgroundServices;
using VideoForensics.Hosting.Remote;
using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Providers.Ring;
using VideoForensics.Providers.Ring.Services;

namespace VideoForensics.Hosting
{
    /// <summary>
    /// Shared DI registration for every VideoForensics host. Extracted from the console app's and
    /// MCP server's near-identical Program.cs registration blocks so there's one place that can
    /// drift instead of two - see the "MAUI Blazor Hybrid + Web App Conversion" plan, section 1.
    /// </summary>
    public static class VideoForensicsHostingExtensions
    {
        /// <summary>
        /// Registers the data access layer only (SQLite, EF Core repositories, Data.Core facade) -
        /// no provider/Ring services. Used by every host that needs a local database: the server-tier
        /// hosts (console, MCP) for their real DB, and eventually a MAUI client for its local
        /// offline-review cache.
        /// </summary>
        public static IServiceCollection AddVideoForensicsDataLayer(this IServiceCollection services)
        {
            services.AddVideoForensicsSqlite();
            services.AddVideoForensicsDatabase();
            services.AddVideoForensicsDataCore();
            return services;
        }

        /// <summary>
        /// Registers every provider's four services (today: Ring's auth/discovery/download/event-config,
        /// per CLAUDE.md's "Adding a New Provider" convention) plus the download-orchestration and
        /// evidence-workflow services built on top of them. Only ever called by a host that is allowed
        /// to talk to a provider directly (console, MCP, and later VideoForensics.WebApp as "the
        /// server") - never by a thin client such as the planned MAUI app.
        /// </summary>
        public static IServiceCollection AddVideoForensicsServerCore(this IServiceCollection services)
        {
            // Shared session provider (must be singleton so all services/scopes observe the same
            // keyed session map - see ISessionProvider's per-account redesign). ICredentialStore is
            // a plain file-based store with no Scoped dependency of its own, safe to stay Singleton.
            services.AddSingleton<ISessionProvider, SessionProvider>();
            services.AddSingleton<ICredentialStore>(new CredentialStore());

            // Ring provider services, with factories providing typed loggers. Scoped, not Singleton
            // (a change from the original console/MCP Program.cs, caught by a DI-graph smoke test
            // during M1): RingAuthService depends on ICredentialRepository/IRingAccountRepository/
            // IProviderAccountRepository/IUserRepository, all Scoped - a Singleton capturing them is
            // the same captive-dependency problem as the services below. Scoped-depending-on-Singleton
            // (ISessionProvider, ICredentialStore) is fine; only the reverse is the bug. For today's
            // single-root-scope console/MCP hosts this is observably identical to Singleton.
            services.AddScoped<IProviderAuthService>(provider =>
                new RingAuthService(
                    provider.GetRequiredService<ILogger<RingAuthService>>(),
                    provider.GetRequiredService<ISessionProvider>(),
                    provider.GetRequiredService<ICredentialStore>(),
                    provider.GetRequiredService<ICredentialRepository>(),
                    provider.GetRequiredService<IRingAccountRepository>(),
                    provider.GetRequiredService<IProviderAccountRepository>(),
                    provider.GetRequiredService<IUserRepository>()
                )
            );
            services.AddScoped<IDeviceDiscoveryService>(provider =>
                new RingDeviceDiscoveryService(
                    provider.GetRequiredService<ILogger<RingDeviceDiscoveryService>>(),
                    provider.GetRequiredService<ISessionProvider>()
                )
            );
            services.AddScoped<IMediaDownloadService>(provider =>
                new RingMediaDownloadService(
                    provider.GetRequiredService<ILogger<RingMediaDownloadService>>(),
                    provider.GetRequiredService<ISessionProvider>(),
                    provider.GetRequiredService<IVideoForensicsDataClient>()
                )
            );
            services.AddScoped<IEventAndConfigService>(provider =>
                new RingEventAndConfigService(
                    provider.GetRequiredService<ILogger<RingEventAndConfigService>>(),
                    provider.GetRequiredService<ISessionProvider>()
                )
            );
            services.AddScoped<IVideoProvider>(provider =>
                new RingVideoProvider(
                    provider.GetRequiredService<ILogger<RingVideoProvider>>(),
                    provider.GetRequiredService<IProviderAuthService>(),
                    provider.GetRequiredService<IDeviceDiscoveryService>(),
                    provider.GetRequiredService<IMediaDownloadService>(),
                    provider.GetRequiredService<IEventAndConfigService>()
                )
            );

            // Runtime configuration. Starts out holding class defaults; the caller loads persisted
            // settings into this same singleton via InitializeVideoForensicsDataAsync below, once the
            // DB is ready - every service already holding a reference observes the loaded values too.
            services.AddSingleton<IForensicsConfiguration>(new ForensicsConfiguration());

            // Scoped, not Singleton, for these four: caught by a DI-graph smoke test during M1 -
            // every one of them transitively depends on a Scoped repository (IAppSettingRepository,
            // IMediaItemRepository, IEventRepository, IDeviceRepository, IActionLogRepository,
            // IVideoForensicsDataClient are all registered TryAddScoped/AddScoped by the data layer).
            // A Singleton capturing a Scoped dependency is a captive-dependency bug: harmless today
            // because console/MCP never create child scopes and never run ValidateScopes, but it
            // would silently break (or, once ValidateOnBuild is on, fail outright) the moment a real
            // per-circuit-scoped host (Blazor Server, per the MAUI/Web plan) exists. Matching
            // JammingToolsOrchestrator's already-Scoped registration below for the same reason.
            services.AddScoped<IVideoDownloadService>(serviceProvider =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<VideoDownloadServiceAdapter>>();
                var videoProvider = serviceProvider.GetRequiredService<IVideoProvider>();
                var authService = serviceProvider.GetRequiredService<IProviderAuthService>();
                var downloadService = serviceProvider.GetRequiredService<IMediaDownloadService>();
                var deviceService = serviceProvider.GetRequiredService<IDeviceDiscoveryService>();
                var dataClient = serviceProvider.GetRequiredService<IVideoForensicsDataClient>();
                var forensicsConfig = serviceProvider.GetRequiredService<IForensicsConfiguration>();
                return new VideoDownloadServiceAdapter(logger, videoProvider, authService, downloadService, deviceService, dataClient, forensicsConfig);
            });

            services.AddScoped<IEvidenceValidationService>(serviceProvider =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<EvidenceValidationOrchestrator>>();
                var eventAndConfigService = serviceProvider.GetRequiredService<IEventAndConfigService>();
                var eventRepository = serviceProvider.GetRequiredService<IEventRepository>();
                var deviceRepository = serviceProvider.GetRequiredService<IDeviceRepository>();
                var integrityService = serviceProvider.GetRequiredService<IIntegrityVerificationService>();
                var mediaItemRepository = serviceProvider.GetRequiredService<IMediaItemRepository>();
                var reconciliationService = serviceProvider.GetRequiredService<IProviderReconciliationService>();
                return new EvidenceValidationOrchestrator(logger, eventAndConfigService, eventRepository, deviceRepository, integrityService, mediaItemRepository, reconciliationService);
            });

            services.AddScoped<IEvidenceExportService>(serviceProvider =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<EvidenceExportOrchestrator>>();
                var mediaItemRepository = serviceProvider.GetRequiredService<IMediaItemRepository>();
                var integrityVerificationService = serviceProvider.GetRequiredService<IIntegrityVerificationService>();
                var actionLogRepository = serviceProvider.GetRequiredService<IActionLogRepository>();
                var exportRecordService = serviceProvider.GetRequiredService<IExportRecordService>();
                return new EvidenceExportOrchestrator(logger, mediaItemRepository, integrityVerificationService, actionLogRepository, exportRecordService);
            });

            services.AddScoped<IForensicsConfigurationService>(serviceProvider =>
                new ForensicsConfigurationService(
                    serviceProvider.GetRequiredService<ILogger<ForensicsConfigurationService>>(),
                    serviceProvider.GetRequiredService<IAppSettingRepository>()
                )
            );

            // Constructor-injected (all dependencies already registered above)
            services.AddScoped<JammingToolsOrchestrator>();
            services.AddScoped<ConfigToolsOrchestrator>();

            // RSSI/device-health background sync (plan §3). IProviderHealthSource is a per-provider
            // optional capability - Ring's is registered here the same way its other four services
            // are; a future provider without health telemetry simply registers nothing and
            // DeviceHealthSyncService skips it. IBatteryStatusProvider defaults to "always on AC" for
            // every server-tier host (console, MCP, WebApp) - only a MAUI client would ever override
            // this, and MAUI never runs this background service in the first place (see its own doc
            // comment). AddHostedService is safe to call from every server-tier host's own
            // AddVideoForensicsServerCore() call site; ASP.NET Core and the generic Host both already
            // de-duplicate re-registrations of the same singleton BackgroundService type.
            services.AddScoped<IProviderHealthSource, RingHealthSource>();
            services.AddSingleton<IBatteryStatusProvider, AlwaysOnAcPower>();
            services.AddHostedService<DeviceHealthSyncService>();

            // Media storage seam (plan §4/M5) - only LocalDiskMediaStorageProvider behind it today.
            services.AddSingleton<IMediaStorageProvider, LocalDiskMediaStorageProvider>();

            // Pairing/RBAC/security-audit backbone (plan §5, M6). IPairingTokenService is
            // per-process in-memory state (short-lived tokens), so it must be Singleton.
            // ISessionTokenService only needs the already-registered IDataProtectionProvider.
            services.AddSingleton<IPairingTokenService, PairingTokenService>();
            services.AddSingleton<IWebAuthnCeremonyCache, WebAuthnCeremonyCache>();
            services.AddSingleton<ISessionTokenService, SessionTokenService>();
            services.AddSingleton<INetworkTierResolver, NetworkTierResolver>();
            services.AddScoped<ISecurityAuditLogger, SecurityAuditLogger>();

            return services;
        }

        /// <summary>
        /// Registers HTTP-backed read-only repository implementations that call the server's Minimal
        /// API (see VideoForensics.WebApp/Api/MediaApiEndpoints.cs) instead of touching a local database
        /// or any provider directly - for a client host (MAUI) that talks to a remote server rather than
        /// being the server itself. Per §1's "only the server pulls from any provider" rule, no client
        /// host calling this method may also call AddVideoForensicsServerCore().
        /// </summary>
        public static IServiceCollection AddVideoForensicsClientApi(this IServiceCollection services, Uri serverAddress)
        {
            services.AddHttpClient<IDeviceRepository, RemoteDeviceRepository>(c => c.BaseAddress = serverAddress);
            services.AddHttpClient<IMediaItemRepository, RemoteMediaItemRepository>(c => c.BaseAddress = serverAddress);
            services.AddHttpClient<IIntegrityRecordRepository, RemoteIntegrityRecordRepository>(c => c.BaseAddress = serverAddress);
            return services;
        }

        /// <summary>
        /// Runs the deferred startup sequence every server-tier host needs after the DI container is
        /// built: apply DB migrations, one-time backfill the Events table from legacy DownloadEvents
        /// history, and load persisted settings into the IForensicsConfiguration singleton.
        /// </summary>
        public static async Task InitializeVideoForensicsDataAsync(IServiceProvider services, ILogger logger, CancellationToken ct)
        {
            var dbFactory = services.GetRequiredService<IDbContextFactory<VideoForensicsDbContext>>();
            await DatabaseInitializer.InitializeAsync(dbFactory, logger, ct);

            // Everything below resolves Scoped services (IAppSettingRepository, IDownloadEventRepository,
            // IMediaItemRepository, IEventRepository, IForensicsConfigurationService) - since the
            // registrations were fixed to be Scoped (see AddVideoForensicsServerCore), they can't be
            // resolved directly from the root `services` provider passed in here (that throws
            // "Cannot resolve scoped service ... from root provider" under strict scope validation,
            // which ASP.NET Core enables by default in the Development environment - caught by
            // actually running VideoForensics.WebApp, not by any build). Create an explicit scope.
            using var scope = services.CreateScope();
            var sp = scope.ServiceProvider;

            const string backfillFlagKey = "EventsBackfillFromDownloadEventsCompleted";
            var appSettingRepo = sp.GetRequiredService<IAppSettingRepository>();
            var alreadyDone = await appSettingRepo.GetAsync(backfillFlagKey, ct);
            if (alreadyDone != "true")
            {
                var downloadEventRepo = sp.GetRequiredService<IDownloadEventRepository>();
                var mediaItemRepo = sp.GetRequiredService<IMediaItemRepository>();
                var eventRepo = sp.GetRequiredService<IEventRepository>();
                var count = await EventBackfillService.BackfillFromDownloadEventsAsync(
                    downloadEventRepo, mediaItemRepo, eventRepo, logger, ct);
                await appSettingRepo.SetAsync(backfillFlagKey, "true", ct);
                logger.LogInformation("Events backfill completed: {Count} record(s).", count);
            }

            var configService = sp.GetRequiredService<IForensicsConfigurationService>();
            var appConfig = services.GetRequiredService<IForensicsConfiguration>() as ForensicsConfiguration
                ?? throw new InvalidOperationException("Configuration must be a ForensicsConfiguration instance");
            await ConfigurationLoader.LoadAndApplyAsync(configService, appConfig, logger, ct);
        }
    }
}
