using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Core;
using VideoForensics.Client.Core.Contracts;
using VideoForensics.Client.Core.Services;
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
using VideoForensics.Hosting.Services;
using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Providers.Common.Helpers.Platform;
using VideoForensics.Providers.Ring;
using VideoForensics.Providers.Ring.Services;
using VideoForensics.Providers.Uniview;
using VideoForensics.Providers.Uniview.Services;
using VideoForensics.Providers.Wyze.Services;

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
        /// Fixed default password for the auto-seeded first SuperAdmin account (Username "admin"), used only
        /// when the Operators table is empty at startup. MustChangePassword is set true on this account, so
        /// it can never be used for anything beyond the very first login. Documented here AND in CLAUDE.md
        /// so an operator can find it without reading source: **ChangeMe123!**
        /// </summary>
        public const string DefaultSuperAdminPassword = "ChangeMe123!";
        /// <summary>
        /// Builds a Ring authentication service for use by multiple registration paths (unkeyed,
        /// multi-provider auth factories, and keyed per-provider services). Centralizes the
        /// construction logic so there's exactly one place per service that builds it.
        /// </summary>
        private static IProviderAuthService BuildRingAuthService(IServiceProvider provider)
        {
            return new RingAuthService(
                provider.GetRequiredService<ILogger<RingAuthService>>(),
                provider.GetRequiredService<ISessionProvider>(),
                provider.GetRequiredService<ICredentialStore>(),
                provider.GetRequiredService<ICredentialRepository>(),
                provider.GetRequiredService<IRingAccountRepository>(),
                provider.GetRequiredService<IProviderAccountRepository>(),
                provider.GetRequiredService<IUserRepository>(),
                provider.GetRequiredService<VideoForensics.Providers.Common.Contracts.INotificationDispatcher>()
            );
        }

        /// <summary>
        /// Builds a Ring device discovery service for use by multiple registration paths.
        /// </summary>
        private static IDeviceDiscoveryService BuildRingDeviceDiscoveryService(IServiceProvider provider)
        {
            return new RingDeviceDiscoveryService(
                provider.GetRequiredService<ILogger<RingDeviceDiscoveryService>>(),
                provider.GetRequiredService<ISessionProvider>()
            );
        }

        /// <summary>
        /// Builds a Ring media download service for use by multiple registration paths.
        /// </summary>
        private static IMediaDownloadService BuildRingMediaDownloadService(IServiceProvider provider)
        {
            return new RingMediaDownloadService(
                provider.GetRequiredService<ILogger<RingMediaDownloadService>>(),
                provider.GetRequiredService<ISessionProvider>(),
                provider.GetRequiredService<IVideoForensicsDataClient>()
            );
        }

        /// <summary>
        /// Builds a Ring event and config service for use by multiple registration paths.
        /// </summary>
        private static IEventAndConfigService BuildRingEventAndConfigService(IServiceProvider provider)
        {
            return new RingEventAndConfigService(
                provider.GetRequiredService<ILogger<RingEventAndConfigService>>(),
                provider.GetRequiredService<ISessionProvider>()
            );
        }

        /// <summary>
        /// Builds a Uniview authentication service for use by multiple registration paths.
        /// </summary>
        private static IProviderAuthService BuildUniviewAuthService(IServiceProvider provider)
        {
            return new UniviewAuthService(
                provider.GetRequiredService<ILogger<UniviewAuthService>>(),
                provider.GetRequiredService<IUniviewSessionProvider>(),
                provider.GetRequiredService<IForensicsConfiguration>(),
                provider.GetRequiredService<ICredentialRepository>()
            );
        }

        /// <summary>
        /// Builds a Uniview device discovery service for use by multiple registration paths.
        /// </summary>
        private static IDeviceDiscoveryService BuildUniviewDeviceDiscoveryService(IServiceProvider provider)
        {
            return new UniviewDeviceDiscoveryService(
                provider.GetRequiredService<ILogger<UniviewDeviceDiscoveryService>>(),
                provider.GetRequiredService<IUniviewSessionProvider>(),
                provider.GetRequiredService<IForensicsConfiguration>()
            );
        }

        /// <summary>
        /// Builds a Uniview media download service for use by multiple registration paths.
        /// </summary>
        private static IMediaDownloadService BuildUniviewMediaDownloadService(IServiceProvider provider)
        {
            return new UniviewMediaDownloadService(
                provider.GetRequiredService<ILogger<UniviewMediaDownloadService>>(),
                provider.GetRequiredService<IUniviewSessionProvider>()
            );
        }

        /// <summary>
        /// Builds a Uniview event and config service for use by multiple registration paths.
        /// </summary>
        private static IEventAndConfigService BuildUniviewEventAndConfigService(IServiceProvider provider)
        {
            return new UniviewEventAndConfigService(
                provider.GetRequiredService<ILogger<UniviewEventAndConfigService>>(),
                provider.GetRequiredService<IUniviewSessionProvider>()
            );
        }

        /// <summary>
        /// Builds a Wyze authentication service for use by multiple registration paths.
        /// </summary>
        private static IProviderAuthService BuildWyzeAuthService(IServiceProvider provider)
        {
            return new WyzeAuthService(
                provider.GetRequiredService<ILogger<WyzeAuthService>>()
            );
        }

        /// <summary>
        /// Builds a Wyze device discovery service for use by multiple registration paths.
        /// </summary>
        private static IDeviceDiscoveryService BuildWyzeDeviceDiscoveryService(IServiceProvider provider)
        {
            return new WyzeDeviceDiscoveryService(
                provider.GetRequiredService<ILogger<WyzeDeviceDiscoveryService>>()
            );
        }

        /// <summary>
        /// Builds a Wyze media download service for use by multiple registration paths.
        /// </summary>
        private static IMediaDownloadService BuildWyzeMediaDownloadService(IServiceProvider provider)
        {
            return new WyzeMediaDownloadService(
                provider.GetRequiredService<ILogger<WyzeMediaDownloadService>>()
            );
        }

        /// <summary>
        /// Builds a Wyze event and config service for use by multiple registration paths.
        /// </summary>
        private static IEventAndConfigService BuildWyzeEventAndConfigService(IServiceProvider provider)
        {
            return new WyzeEventAndConfigService(
                provider.GetRequiredService<ILogger<WyzeEventAndConfigService>>()
            );
        }

        /// <summary>
        /// Builds a notification dispatcher for use by multiple registration paths.
        /// </summary>
        private static INotificationDispatcher BuildNotificationDispatcher(IServiceProvider provider)
        {
            return new NotificationDispatcher(
                provider.GetRequiredService<IEnumerable<INotificationProvider>>(),
                provider.GetRequiredService<ILogger<NotificationDispatcher>>(),
                provider.GetRequiredService<INoticeRepository>()
            );
        }

        /// <summary>
        /// Registers the data access layer only (SQLite, EF Core repositories, Data.Core facade) -
        /// no provider/Ring services. Used by every host that needs a local database: the server-tier
        /// hosts (console, MCP) for their real DB, and eventually a MAUI client for its local
        /// offline-review cache.
        /// </summary>
        /// <param name="services">The service collection to register into.</param>
        /// <param name="dbPath">Optional override for the SQLite database file path; defaults to %ProgramData%\VideoForensics\videoforensics.db (see AddVideoForensicsSqlite).</param>
        public static IServiceCollection AddVideoForensicsDataLayer(this IServiceCollection services, string? dbPath = null)
        {
            _ = services.AddVideoForensicsSqlite(dbPath);
            _ = services.AddVideoForensicsDatabase();
            _ = services.AddVideoForensicsDataCore();

            // App-lock (plan §5.9) is device-local and only meaningful on MAUI - every host gets
            // this no-op default; VideoForensics.MauiApp registers the real Preferences-backed
            // implementation afterward, which wins by DI's last-registration-wins rule.
            _ = services.AddSingleton<IAppLockPreferencesStore, NullAppLockPreferencesStore>();

            return services;
        }

        /// <summary>
        /// Registers the selected provider's four services (auth/discovery/download/event-config,
        /// per CLAUDE.md's "Adding a New Provider" convention) plus the download-orchestration and
        /// evidence-workflow services built on top of them. Today supports "Ring" (default) or "Uniview".
        /// Only ever called by a host that is allowed to talk to a provider directly (console, MCP, and
        /// later VideoForensics.WebApp as "the server") - never by a thin client such as the planned MAUI app.
        /// </summary>
        /// <param name="services">The service collection to register into.</param>
        /// <param name="activeProviderName">Name of the active provider ("Ring" or "Uniview"); defaults to "Ring" for backward compatibility.</param>
        public static IServiceCollection AddVideoForensicsServerCore(this IServiceCollection services, string activeProviderName = "Ring")
        {
            // MainLayout.razor (rendered by every host sharing Ui.Shared, WebApp included) @injects
            // IServerConnectivityState/IServerLocationInformationService - these were only ever
            // registered by AddVideoForensicsClientApi() for client hosts (MAUI), so a server-tier
            // host calling AddVideoForensicsServerCore() alone had nothing to satisfy that injection
            // and threw at render time. TryAddSingleton-based, so this is a safe default for the
            // server's own UI (which is never "unreachable" from its own perspective) and won't
            // conflict if a client host's own registrations also call this.
            _ = services.AddServerLocationServices();
            _ = services.AddSingleton<IStorageLocationProvider, StorageLocationProvider>();

            // Shared session providers (must be singleton so all services/scopes observe the same
            // keyed session map - see ISessionProvider's per-account redesign). ICredentialStore is
            // a plain file-based store with no Scoped dependency of its own, safe to stay Singleton.
            // Both Ring and Uniview session providers are registered unconditionally - no harm even
            // if the active provider doesn't use one, and future multi-provider support may need both.
            _ = services.AddSingleton<ISessionProvider, SessionProvider>();
            _ = services.AddSingleton<IUniviewSessionProvider, UniviewSessionProvider>();
            _ = services.AddSingleton<ICredentialStore>(new CredentialStore());

            // Provider services, with factories providing typed loggers. Scoped, not Singleton
            // (a change from the original console/MCP Program.cs, caught by a DI-graph smoke test
            // during M1): each auth service depends on Scoped repositories - a Singleton capturing
            // them is the same captive-dependency problem as the services below. Scoped-depending-on-
            // Singleton (ISessionProvider, ICredentialStore, IUniviewSessionProvider) is fine; only
            // the reverse is the bug. For today's single-root-scope console/MCP hosts this is observably
            // identical to Singleton.
            if (string.Equals(activeProviderName, "Ring", StringComparison.OrdinalIgnoreCase))
            {
                _ = services.AddScoped<IProviderAuthService>(BuildRingAuthService);
                _ = services.AddScoped<IDeviceDiscoveryService>(BuildRingDeviceDiscoveryService);
                _ = services.AddScoped<IMediaDownloadService>(BuildRingMediaDownloadService);
                _ = services.AddScoped<IEventAndConfigService>(BuildRingEventAndConfigService);
                _ = services.AddScoped<IVideoProvider>(provider =>
                    new RingVideoProvider(
                        provider.GetRequiredService<ILogger<RingVideoProvider>>(),
                        provider.GetRequiredService<IProviderAuthService>(),
                        provider.GetRequiredService<IDeviceDiscoveryService>(),
                        provider.GetRequiredService<IMediaDownloadService>(),
                        provider.GetRequiredService<IEventAndConfigService>()
                    )
                );

                // Ring self-test orchestrator and service: run state must survive across scoped requests,
                // so registered as Singleton (same reasoning as ISessionProvider). Registered against
                // its interface, not the concrete type, since LocalRingSelfTestService depends on
                // IRingSelfTestOrchestrator.
                _ = services.AddSingleton<IRingSelfTestOrchestrator, RingSelfTestOrchestrator>();
                _ = services.AddScoped<IRingSelfTestService, LocalRingSelfTestService>();
            }
            else if (string.Equals(activeProviderName, "Uniview", StringComparison.OrdinalIgnoreCase))
            {
                _ = services.AddScoped<IProviderAuthService>(BuildUniviewAuthService);
                _ = services.AddScoped<IDeviceDiscoveryService>(BuildUniviewDeviceDiscoveryService);
                _ = services.AddScoped<IMediaDownloadService>(BuildUniviewMediaDownloadService);
                _ = services.AddScoped<IEventAndConfigService>(BuildUniviewEventAndConfigService);
                _ = services.AddScoped<IVideoProvider>(provider =>
                    new UniviewVideoProvider(
                        provider.GetRequiredService<ILogger>(),
                        provider.GetRequiredService<IProviderAuthService>(),
                        provider.GetRequiredService<IDeviceDiscoveryService>(),
                        provider.GetRequiredService<IMediaDownloadService>(),
                        provider.GetRequiredService<IEventAndConfigService>()
                    )
                );
            }
            else
            {
                throw new InvalidOperationException($"Unknown ActiveProvider '{activeProviderName}' - expected 'Ring' or 'Uniview'.");
            }

            // Independent of the single "active" provider above: lets "Add Account" authenticate against
            // any registered provider by name, not just whichever one this server booted with.
            var multiProviderAuthFactories = new Dictionary<string, Func<IServiceProvider, IProviderAuthService>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Ring"] = BuildRingAuthService,
                ["Uniview"] = BuildUniviewAuthService,
                ["Wyze"] = BuildWyzeAuthService,
            };
            _ = services.AddScoped<IMultiProviderAuthService>(provider => new MultiProviderAuthService(provider, multiProviderAuthFactories));

            // Runtime configuration. Starts out holding class defaults; the caller loads persisted
            // settings into this same singleton via InitializeVideoForensicsDataAsync below, once the
            // DB is ready - every service already holding a reference observes the loaded values too.
            _ = services.AddSingleton<IForensicsConfiguration>(new ForensicsConfiguration());

            // Scoped, not Singleton, for these four: caught by a DI-graph smoke test during M1 -
            // every one of them transitively depends on a Scoped repository (IAppSettingRepository,
            // IMediaItemRepository, IEventRepository, IDeviceRepository, IActionLogRepository,
            // IVideoForensicsDataClient are all registered TryAddScoped/AddScoped by the data layer).
            // A Singleton capturing a Scoped dependency is a captive-dependency bug: harmless today
            // because console/MCP never create child scopes and never run ValidateScopes, but it
            // would silently break (or, once ValidateOnBuild is on, fail outright) the moment a real
            // per-circuit-scoped host (Blazor Server, per the MAUI/Web plan) exists. Matching
            // JammingToolsOrchestrator's already-Scoped registration below for the same reason.
            _ = services.AddScoped<IVideoDownloadService>(serviceProvider =>
            {
                ILogger<VideoDownloadServiceAdapter> logger = serviceProvider.GetRequiredService<ILogger<VideoDownloadServiceAdapter>>();
                IVideoProvider videoProvider = serviceProvider.GetRequiredService<IVideoProvider>();
                IProviderAuthService authService = serviceProvider.GetRequiredService<IProviderAuthService>();
                IMediaDownloadService downloadService = serviceProvider.GetRequiredService<IMediaDownloadService>();
                IDeviceDiscoveryService deviceService = serviceProvider.GetRequiredService<IDeviceDiscoveryService>();
                IVideoForensicsDataClient dataClient = serviceProvider.GetRequiredService<IVideoForensicsDataClient>();
                IForensicsConfiguration forensicsConfig = serviceProvider.GetRequiredService<IForensicsConfiguration>();
                return new VideoDownloadServiceAdapter(logger, videoProvider, authService, downloadService, deviceService, dataClient, forensicsConfig);
            });

            _ = services.AddScoped<IEvidenceValidationService>(serviceProvider =>
            {
                ILogger<EvidenceValidationOrchestrator> logger = serviceProvider.GetRequiredService<ILogger<EvidenceValidationOrchestrator>>();
                IEventAndConfigService eventAndConfigService = serviceProvider.GetRequiredService<IEventAndConfigService>();
                IEventRepository eventRepository = serviceProvider.GetRequiredService<IEventRepository>();
                IDeviceRepository deviceRepository = serviceProvider.GetRequiredService<IDeviceRepository>();
                IIntegrityVerificationService integrityService = serviceProvider.GetRequiredService<IIntegrityVerificationService>();
                IMediaItemRepository mediaItemRepository = serviceProvider.GetRequiredService<IMediaItemRepository>();
                IProviderReconciliationService reconciliationService = serviceProvider.GetRequiredService<IProviderReconciliationService>();
                return new EvidenceValidationOrchestrator(logger, eventAndConfigService, eventRepository, deviceRepository, integrityService, mediaItemRepository, reconciliationService);
            });

            _ = services.AddScoped<IEvidenceExportService>(serviceProvider =>
            {
                ILogger<EvidenceExportOrchestrator> logger = serviceProvider.GetRequiredService<ILogger<EvidenceExportOrchestrator>>();
                IMediaItemRepository mediaItemRepository = serviceProvider.GetRequiredService<IMediaItemRepository>();
                IIntegrityVerificationService integrityVerificationService = serviceProvider.GetRequiredService<IIntegrityVerificationService>();
                IActionLogRepository actionLogRepository = serviceProvider.GetRequiredService<IActionLogRepository>();
                IExportRecordService exportRecordService = serviceProvider.GetRequiredService<IExportRecordService>();
                return new EvidenceExportOrchestrator(logger, mediaItemRepository, integrityVerificationService, actionLogRepository, exportRecordService);
            });

            // Backup export/import (event reimport feature): embeds/reads a DB record GUID in a
            // media file's own container metadata (independent of the JSON sidecar), and exports/
            // imports the whole database as portable JSON+zip, so evidence can be reconstituted into
            // a fresh database on another machine.
            _ = services.AddScoped<IMediaMetadataTagger>(serviceProvider =>
                new FfmpegMediaMetadataTagger(serviceProvider.GetRequiredService<ILogger<FfmpegMediaMetadataTagger>>())
            );

            _ = services.AddScoped<IBackupExportService>(serviceProvider =>
            {
                ILogger<BackupExportOrchestrator> logger = serviceProvider.GetRequiredService<ILogger<BackupExportOrchestrator>>();
                IProviderAccountRepository providerAccountRepository = serviceProvider.GetRequiredService<IProviderAccountRepository>();
                ILocationRepository locationRepository = serviceProvider.GetRequiredService<ILocationRepository>();
                IDeviceRepository deviceRepository = serviceProvider.GetRequiredService<IDeviceRepository>();
                IEventRepository eventRepository = serviceProvider.GetRequiredService<IEventRepository>();
                IDownloadEventRepository downloadEventRepository = serviceProvider.GetRequiredService<IDownloadEventRepository>();
                IMediaItemRepository backupMediaItemRepository = serviceProvider.GetRequiredService<IMediaItemRepository>();
                IForensicsConfiguration forensicsConfiguration = serviceProvider.GetRequiredService<IForensicsConfiguration>();
                IMediaMetadataTagger metadataTagger = serviceProvider.GetRequiredService<IMediaMetadataTagger>();
                return new BackupExportOrchestrator(logger, providerAccountRepository, locationRepository, deviceRepository, eventRepository, downloadEventRepository, backupMediaItemRepository, forensicsConfiguration, metadataTagger);
            });

            _ = services.AddScoped<IBackupImportService>(serviceProvider =>
                new BackupImportOrchestrator(
                    serviceProvider.GetRequiredService<ILogger<BackupImportOrchestrator>>(),
                    serviceProvider.GetRequiredService<IUnitOfWork>()
                )
            );

            _ = services.AddScoped<IForensicsConfigurationService>(serviceProvider =>
                new ForensicsConfigurationService(
                    serviceProvider.GetRequiredService<ILogger<ForensicsConfigurationService>>(),
                    serviceProvider.GetRequiredService<IAppSettingRepository>()
                )
            );

            // Constructor-injected (all dependencies already registered above)
            _ = services.AddScoped<JammingToolsOrchestrator>();
            _ = services.AddScoped<ConfigToolsOrchestrator>();

            // RSSI/device-health background sync (plan §3). IProviderHealthSource is a per-provider
            // optional capability - Ring's is registered here the same way its other four services
            // are; a future provider without health telemetry simply registers nothing and
            // DeviceHealthSyncService skips it. IBatteryStatusProvider defaults to "always on AC" for
            // every server-tier host (console, MCP, WebApp) - only a MAUI client would ever override
            // this, and MAUI never runs this background service in the first place (see its own doc
            // comment). AddHostedService is safe to call from every server-tier host's own
            // AddVideoForensicsServerCore() call site; ASP.NET Core and the generic Host both already
            // de-duplicate re-registrations of the same singleton BackgroundService type.
            _ = services.AddScoped<IProviderHealthSource, RingHealthSource>();
            _ = services.AddSingleton<IBatteryStatusProvider, AlwaysOnAcPower>();
            _ = services.AddHostedService<DeviceHealthSyncService>();

            // Update-check background service (plan §3). Periodically polls GitHub for a newer release
            // and either notifies or auto-downloads/launches the installer based on configuration.
            _ = services.AddHttpClient<IGitHubReleaseClient, GitHubReleaseClient>(client =>
            {
                client.BaseAddress = new Uri("https://api.github.com/");
                client.DefaultRequestHeaders.UserAgent.ParseAdd("VideoForensics-UpdateCheck/1.0");
            });
            _ = services.AddSingleton<IUpdateInstaller, UpdateInstaller>();
            _ = services.AddSingleton<Client.Common.Contracts.IUpdateCheckService, VideoForensics.Hosting.BackgroundServices.UpdateCheckService>();
            _ = services.AddHostedService(sp => (VideoForensics.Hosting.BackgroundServices.UpdateCheckService)sp.GetRequiredService<Client.Common.Contracts.IUpdateCheckService>());

            // Media storage seam (plan §4/M5) - only LocalDiskMediaStorageProvider behind it today.
            _ = services.AddSingleton<IMediaStorageProvider, LocalDiskMediaStorageProvider>();

            // Pairing/RBAC/security-audit backbone (plan §5, M6). IPairingTokenService is
            // per-process in-memory state (short-lived tokens), so it must be Singleton.
            // ISessionTokenService only needs the already-registered IDataProtectionProvider.
            // IDeviceCodePairingService is also per-process in-memory state (10-min sessions).
            _ = services.AddSingleton<IPairingTokenService, PairingTokenService>();
            _ = services.AddSingleton<IDeviceCodePairingService, DeviceCodePairingService>();
            _ = services.AddSingleton<IWebAuthnCeremonyCache, WebAuthnCeremonyCache>();
            _ = services.AddSingleton<ISessionTokenService, SessionTokenService>();
            _ = services.AddSingleton<IStepUpAuthService, StepUpAuthService>();
            _ = services.AddSingleton<INetworkTierResolver, NetworkTierResolver>();
            _ = services.AddScoped<ISecurityAuditLogger, SecurityAuditLogger>();
            _ = services.AddScoped<IProviderApiBudgetGuard, ProviderApiBudgetGuard>();

            // Urgent notifications (plan §5.6) - fanned out from SecurityAuditLogger itself, not
            // from individual call sites, so a new urgent event type never needs a second wire-up.
            // Email is the one channel built so far (the plan's stated reliable baseline); Web Push
            // is now also implemented - MAUI toast remains deliberately not yet implemented (see
            // INotificationProvider's doc comment for why the extensibility point exists regardless).
            _ = services.AddScoped<ISmtpPasswordStore, SmtpPasswordStore>();
            _ = services.AddScoped<IUrgencyOverrideStore, UrgencyOverrideStore>();
            _ = services.AddScoped<INotificationProvider, EmailNotificationProvider>();
            _ = services.AddScoped<EmailNotificationProvider>();
            _ = services.AddScoped<INotificationDispatcher>(BuildNotificationDispatcher);

            return services;
        }

        /// <summary>
        /// Registers keyed provider services for each enabled provider, allowing background/scheduled-task
        /// code to resolve services by provider name via [FromKeyedServices(providerName)] or
        /// IServiceProvider.GetRequiredKeyedService{T}(providerName). This is a sibling to
        /// AddVideoForensicsServerCore (which stays as-is for MAUI/UI interactive workflows); the
        /// unkeyed single-active-provider registration remains the canonical per-scope default.
        /// </summary>
        /// <param name="services">The service collection to register into.</param>
        /// <param name="enabledProviders">List of provider names to register (e.g., ["Ring", "Wyze"]); case-insensitive matching.</param>
        public static IServiceCollection AddVideoForensicsMultiProviderServices(
            this IServiceCollection services, IEnumerable<string> enabledProviders)
        {
            foreach (string providerName in enabledProviders ?? Enumerable.Empty<string>())
            {
                if (string.Equals(providerName, "Ring", StringComparison.OrdinalIgnoreCase))
                {
                    _ = services.AddKeyedScoped<IProviderAuthService>(providerName, (provider, key) => BuildRingAuthService(provider));
                    _ = services.AddKeyedScoped<IDeviceDiscoveryService>(providerName, (provider, key) => BuildRingDeviceDiscoveryService(provider));
                    _ = services.AddKeyedScoped<IMediaDownloadService>(providerName, (provider, key) => BuildRingMediaDownloadService(provider));
                    _ = services.AddKeyedScoped<IEventAndConfigService>(providerName, (provider, key) => BuildRingEventAndConfigService(provider));
                    _ = services.AddKeyedScoped<IProviderHealthSource, RingHealthSource>(
                        providerName,
                        (provider, key) =>
                        {
                            var logger = provider.GetRequiredService<ILogger<RingHealthSource>>();
                            var sessionProvider = provider.GetRequiredService<ISessionProvider>();
                            var accountRepository = provider.GetRequiredService<VideoForensics.Data.Common.Contracts.IProviderAccountRepository>();
                            // Must be resolved keyed by "Ring", not the unkeyed IProviderAuthService - this loop
                            // registers Ring/Uniview/Wyze side by side, and the unkeyed overload only ever
                            // returns whichever provider's factory happened to run last in the loop above.
                            var authService = provider.GetRequiredKeyedService<VideoForensics.Providers.Common.Contracts.IProviderAuthService>(providerName);
                            return new RingHealthSource(logger, sessionProvider, accountRepository, authService);
                        });
                }
                else if (string.Equals(providerName, "Uniview", StringComparison.OrdinalIgnoreCase))
                {
                    _ = services.AddKeyedScoped<IProviderAuthService>(providerName, (provider, key) => BuildUniviewAuthService(provider));
                    _ = services.AddKeyedScoped<IDeviceDiscoveryService>(providerName, (provider, key) => BuildUniviewDeviceDiscoveryService(provider));
                    _ = services.AddKeyedScoped<IMediaDownloadService>(providerName, (provider, key) => BuildUniviewMediaDownloadService(provider));
                    _ = services.AddKeyedScoped<IEventAndConfigService>(providerName, (provider, key) => BuildUniviewEventAndConfigService(provider));
                }
                else if (string.Equals(providerName, "Wyze", StringComparison.OrdinalIgnoreCase))
                {
                    _ = services.AddKeyedScoped<IProviderAuthService>(providerName, (provider, key) => BuildWyzeAuthService(provider));
                    _ = services.AddKeyedScoped<IDeviceDiscoveryService>(providerName, (provider, key) => BuildWyzeDeviceDiscoveryService(provider));
                    _ = services.AddKeyedScoped<IMediaDownloadService>(providerName, (provider, key) => BuildWyzeMediaDownloadService(provider));
                    _ = services.AddKeyedScoped<IEventAndConfigService>(providerName, (provider, key) => BuildWyzeEventAndConfigService(provider));
                }
                else
                {
                    throw new InvalidOperationException($"Unknown provider '{providerName}' in AddVideoForensicsMultiProviderServices - expected 'Ring', 'Uniview', or 'Wyze'.");
                }
            }

            return services;
        }

        /// <summary>
        /// Registers server location and connectivity state services (Task 1 & 2) that display
        /// server connection information in the UI and detect offline states. Defaults to no-op
        /// implementations suitable for the server itself; client hosts (MAUI) override these
        /// with their own implementations that use local discovery and connectivity tracking.
        /// </summary>
        public static IServiceCollection AddServerLocationServices(this IServiceCollection services)
        {
            // Register default implementations for hosts where server location info is not tracked
            // (e.g., WebApp where the current process IS the server). MAUI and other clients
            // override these with their own implementations after this call.
            services.TryAddSingleton<VideoForensics.Ui.Shared.Services.IServerLocationInformationService, VideoForensics.Ui.Shared.Services.DefaultServerLocationInformationService>();
            services.TryAddSingleton<VideoForensics.Ui.Shared.Services.IServerConnectivityState, VideoForensics.Ui.Shared.Services.DefaultServerConnectivityState>();

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
            _ = services.AddServerLocationServices();

            // Register the paired-device auth handler as transient, then configure it as a global default
            // for all HTTP clients registered below. This single handler attaches the bearer token to every
            // outgoing request from all 17 Remote* classes without requiring individual auth logic in each one.
            _ = services.AddTransient<PairedDeviceAuthHandler>();
            _ = services.ConfigureHttpClientDefaults(http => http.AddHttpMessageHandler<PairedDeviceAuthHandler>());

            _ = services.AddHttpClient<IDeviceRepository, RemoteDeviceRepository>(c => c.BaseAddress = serverAddress);
            _ = services.AddHttpClient<IMediaItemRepository, RemoteMediaItemRepository>(c => c.BaseAddress = serverAddress);
            _ = services.AddHttpClient<IIntegrityRecordRepository, RemoteIntegrityRecordRepository>(c => c.BaseAddress = serverAddress);
            _ = services.AddHttpClient<IProviderAuthService, RemoteProviderAuthService>(c => c.BaseAddress = serverAddress);
            _ = services.AddHttpClient<IMultiProviderAuthService, RemoteMultiProviderAuthService>(c => c.BaseAddress = serverAddress);
            _ = services.AddHttpClient<IReportGenerationService, RemoteReportGenerationService>(c => c.BaseAddress = serverAddress);
            _ = services.AddHttpClient<IEvidenceValidationService, RemoteEvidenceValidationService>(c => c.BaseAddress = serverAddress);
            _ = services.AddHttpClient<IEvidenceExportService, RemoteEvidenceExportService>(c => c.BaseAddress = serverAddress);
            _ = services.AddHttpClient<IBackupExportService, RemoteBackupExportService>(c => c.BaseAddress = serverAddress);
            _ = services.AddHttpClient<IBackupImportService, RemoteBackupImportService>(c => c.BaseAddress = serverAddress);
            _ = services.AddHttpClient<IDeviceConfigRepository, RemoteDeviceConfigRepository>(c => c.BaseAddress = serverAddress);
            _ = services.AddHttpClient<IEventRepository, RemoteEventRepository>(c => c.BaseAddress = serverAddress);
            _ = services.AddHttpClient<ILegalHoldRepository, RemoteLegalHoldRepository>(c => c.BaseAddress = serverAddress);
            _ = services.AddHttpClient<IUserRepository, RemoteUserRepository>(c => c.BaseAddress = serverAddress);
            _ = services.AddHttpClient<IProviderAccountRepository, RemoteProviderAccountRepository>(c => c.BaseAddress = serverAddress);
            _ = services.AddHttpClient<IDeviceDiscoveryService, RemoteDeviceDiscoveryService>(c => c.BaseAddress = serverAddress);
            _ = services.AddHttpClient<IEventAndConfigService, RemoteEventAndConfigService>(c => c.BaseAddress = serverAddress);
            _ = services.AddHttpClient<IForensicsConfigurationService, RemoteForensicsConfigurationService>(c => c.BaseAddress = serverAddress);
            _ = services.AddHttpClient<IVideoDownloadService, RemoteVideoDownloadService>(c => c.BaseAddress = serverAddress);
            _ = services.AddHttpClient<IRingSelfTestService, RemoteRingSelfTestService>(c => c.BaseAddress = serverAddress);
            _ = services.AddHttpClient<IStorageSettingsService, RemoteStorageSettingsService>(c => c.BaseAddress = serverAddress);
            _ = services.AddHttpClient<Client.Common.Contracts.IUpdateCheckService, Remote.RemoteUpdateCheckService>(c => c.BaseAddress = serverAddress);

            // Real-time push channel for download progress and urgent events (plan §6) - the caller
            // (MAUI or other client) is responsible for calling StartAsync() when a valid session
            // token is available and they wish to begin receiving updates.
            _ = services.AddSingleton<ILiveHubConnection>(sp => new LiveHubConnection(serverAddress, sp));

            return services;
        }

        /// <summary>
        /// Runs the deferred startup sequence every server-tier host needs after the DI container is
        /// built: apply DB migrations, one-time backfill the Events table from legacy DownloadEvents
        /// history, and load persisted settings into the IForensicsConfiguration singleton.
        /// </summary>
        public static async Task InitializeVideoForensicsDataAsync(IServiceProvider services, ILogger logger, CancellationToken ct)
        {
            IDbContextFactory<VideoForensicsDbContext> dbFactory = services.GetRequiredService<IDbContextFactory<VideoForensicsDbContext>>();
            await DatabaseInitializer.InitializeAsync(dbFactory, logger, ct);

            // Everything below resolves Scoped services (IAppSettingRepository, IDownloadEventRepository,
            // IMediaItemRepository, IEventRepository, IForensicsConfigurationService) - since the
            // registrations were fixed to be Scoped (see AddVideoForensicsServerCore), they can't be
            // resolved directly from the root `services` provider passed in here (that throws
            // "Cannot resolve scoped service ... from root provider" under strict scope validation,
            // which ASP.NET Core enables by default in the Development environment - caught by
            // actually running VideoForensics.WebApp, not by any build). Create an explicit scope.
            using IServiceScope scope = services.CreateScope();
            IServiceProvider sp = scope.ServiceProvider;

            const string backfillFlagKey = "EventsBackfillFromDownloadEventsCompleted";
            IAppSettingRepository appSettingRepo = sp.GetRequiredService<IAppSettingRepository>();
            string? alreadyDone = await appSettingRepo.GetAsync(backfillFlagKey, ct);
            if (alreadyDone != "true")
            {
                IDownloadEventRepository downloadEventRepo = sp.GetRequiredService<IDownloadEventRepository>();
                IMediaItemRepository mediaItemRepo = sp.GetRequiredService<IMediaItemRepository>();
                IEventRepository eventRepo = sp.GetRequiredService<IEventRepository>();
                int count = await EventBackfillService.BackfillFromDownloadEventsAsync(
                    downloadEventRepo, mediaItemRepo, eventRepo, logger, ct);
                await appSettingRepo.SetAsync(backfillFlagKey, "true", ct);
                logger.LogInformation("Events backfill completed: {Count} record(s).", count);
            }

            IForensicsConfigurationService configService = sp.GetRequiredService<IForensicsConfigurationService>();
            ForensicsConfiguration appConfig = services.GetRequiredService<IForensicsConfiguration>() as ForensicsConfiguration
                ?? throw new InvalidOperationException("Configuration must be a ForensicsConfiguration instance");
            await ConfigurationLoader.LoadAndApplyAsync(configService, appConfig, logger, ct);

            // Seed a default SuperAdmin account if the Operators table is empty - but only when
            // explicitly opted into via VIDEOFORENSICS_ENABLE_DEFAULT_ADMIN=true (headless/scripted
            // deployments that can't drive a browser). By default this is left off: the interactive
            // /setup wizard (SetupEndpoints.cs) handles first-run admin creation instead, letting the
            // installing user pick their own username/password rather than getting the fixed
            // admin/ChangeMe123! account.
            IOperatorRepository operatorRepo = sp.GetRequiredService<IOperatorRepository>();
            bool enableDefaultAdmin = string.Equals(
                Environment.GetEnvironmentVariable("VIDEOFORENSICS_ENABLE_DEFAULT_ADMIN"),
                "true", StringComparison.OrdinalIgnoreCase);
            if (enableDefaultAdmin && await operatorRepo.IsEmptyAsync(ct))
            {
                var passwordHasher = new Microsoft.AspNetCore.Identity.PasswordHasher<VideoForensics.Data.Common.Entities.Operator>();
                var defaultAdmin = new VideoForensics.Data.Common.Entities.Operator
                {
                    Id = Guid.NewGuid(),
                    Username = "admin",
                    DisplayName = "Administrator",
                    FirstName = "Super",
                    LastName = "Admin",
                    Email = "admin@localhost.invalid",
                    Role = VideoForensics.Data.Common.Entities.OperatorRole.SuperAdmin,
                    IsApproved = true,
                    Active = true,
                    MustChangePassword = true,
                    CreatedAtUtc = DateTime.UtcNow,
                    PasswordUpdatedAtUtc = DateTime.UtcNow,
                    SecurityStamp = Guid.NewGuid()
                };
                defaultAdmin.PasswordHash = passwordHasher.HashPassword(defaultAdmin, DefaultSuperAdminPassword);
                _ = await operatorRepo.AddAsync(defaultAdmin, ct);
                logger.LogWarning(
                    "No operators existed - seeded a default SuperAdmin account. Username: '{Username}', password: the fixed default documented in CLAUDE.md (DefaultSuperAdminPassword constant). You MUST sign in and change this password immediately.",
                    defaultAdmin.Username);
            }
        }
    }
}
