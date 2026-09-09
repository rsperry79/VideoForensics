using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using VideoForensics.Core.Logging.Contracts;
using VideoForensics.Core.Logging.DependencyInjection;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Data.Core.Services;

namespace VideoForensics.Data.Core.DependencyInjection
{
    /// <summary>Dependency injection extensions for Data.Core services.</summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>Adds all Data.Core services to the service collection.</summary>
        public static IServiceCollection AddVideoForensicsDataCore(this IServiceCollection services, int? retentionDays = null)
        {
            // Phase 0 path migration (one-time on startup) - Scoped because it depends on Scoped repositories
            _ = services.AddScoped<IPathMigrationService>(provider =>
                new PathMigrationService(
                    provider.GetRequiredService<ILogger<PathMigrationService>>(),
                    provider.GetRequiredService<IUnitOfWork>(),
                    provider.GetRequiredService<IMediaItemRepository>()
                )
            );

            // Phase 1 core services
            _ = services.AddScoped<IWatermarkService, WatermarkService>();
            _ = services.AddActionLogger();
            _ = services.AddScoped<IVideoForensicsDataClient, VideoForensicsDataClient>();

            // IntegrityVerificationService implementation (implements IIntegrityVerificationService from Data.Common)
            _ = services.AddScoped<IIntegrityVerificationService, IntegrityVerificationService>();

            // Phase 2 reporting and retention services
            _ = services.AddScoped<IReportGenerationService, ReportGenerationService>();

            // Register RetentionService with configurable retention days
            _ = services.AddScoped<IRetentionService>(sp =>
            {
                ILogger<RetentionService> logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RetentionService>>();
                IMediaItemRepository mediaItemRepo = sp.GetRequiredService<VideoForensics.Data.Common.Contracts.IMediaItemRepository>();
                ILegalHoldRepository legalHoldRepo = sp.GetRequiredService<VideoForensics.Data.Common.Contracts.ILegalHoldRepository>();
                IUnitOfWork unitOfWork = sp.GetRequiredService<VideoForensics.Data.Common.Contracts.IUnitOfWork>();
                IActionLogger actionLogger = sp.GetRequiredService<IActionLogger>();
                var days = retentionDays ?? 90;
                return new RetentionService(mediaItemRepo, legalHoldRepo, unitOfWork, actionLogger, logger, days);
            });

            _ = services.AddScoped<IRedactionService, RedactionService>();

            // Phase 3 reconciliation service
            _ = services.AddScoped<IProviderReconciliationService, ProviderReconciliationService>();

            // Phase 4 export service
            _ = services.AddScoped<IExportRecordService, ExportRecordService>();

            return services;
        }
    }
}
