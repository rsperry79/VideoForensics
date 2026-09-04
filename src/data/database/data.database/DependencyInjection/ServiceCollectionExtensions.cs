using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Database.Configurations;
using VideoForensics.Data.Database.Repositories;

namespace VideoForensics.Data.Database.DependencyInjection
{
    /// <summary>Extension methods for registering VideoForensics data access layer dependencies.</summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds VideoForensics database layer services to the dependency injection container.
        /// Registers all repository implementations, the unit of work, and credential encryption.
        /// </summary>
        public static IServiceCollection AddVideoForensicsDatabase(this IServiceCollection services)
        {
            // Ensure data protection is available (used by CredentialEncryptionProvider)
            services.AddDataProtection();

            // Register credential encryption provider
            services.TryAddScoped<ICredentialEncryptionProvider, CredentialEncryptionProvider>();

            // Register repository implementations (per-call pattern with IDbContextFactory)
            services.TryAddScoped<IUserRepository, UserRepository>();
            services.TryAddScoped<IProviderAccountRepository, ProviderAccountRepository>();
            services.TryAddScoped<IRingAccountRepository, RingAccountRepository>();
            services.TryAddScoped<ILocationRepository, LocationRepository>();
            services.TryAddScoped<IDeviceRepository, DeviceRepository>();
            services.TryAddScoped<IMediaItemRepository, MediaItemRepository>();
            services.TryAddScoped<IDownloadEventRepository, DownloadEventRepository>();
            services.TryAddScoped<ICredentialRepository, CredentialRepository>();
            services.TryAddScoped<IEventRepository, EventRepository>();
            services.TryAddScoped<IDeviceConfigRepository, DeviceConfigRepository>();
            services.TryAddScoped<IDeviceHealthSnapshotRepository, DeviceHealthSnapshotRepository>();
            services.TryAddScoped<IAnnotationRepository, AnnotationRepository>();
            services.TryAddScoped<IProviderReconciliationRepository, ProviderReconciliationRepository>();
            services.TryAddScoped<IExportRecordRepository, ExportRecordRepository>();
            services.TryAddScoped<IActionLogRepository, ActionLogRepository>();
            services.TryAddScoped<IAppSettingRepository, AppSettingRepository>();
            services.TryAddScoped<IJammingRepository, JammingRepository>();
            services.TryAddScoped<IIntegrityRecordRepository, IntegrityRecordRepository>();
            services.TryAddScoped<ILegalHoldRepository, LegalHoldRepository>();
            services.TryAddScoped<IOperatorRepository, OperatorRepository>();
            services.TryAddScoped<IPairedDeviceRepository, PairedDeviceRepository>();
            services.TryAddScoped<ISecurityAuditLogRepository, SecurityAuditLogRepository>();
            services.TryAddScoped<IProviderApiCallLogRepository, ProviderApiCallLogRepository>();

            // Register unit of work
            services.TryAddScoped<IUnitOfWork, UnitOfWork>();

            return services;
        }
    }
}
