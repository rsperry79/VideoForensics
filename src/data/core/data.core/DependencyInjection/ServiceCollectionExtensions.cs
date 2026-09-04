using Microsoft.Extensions.DependencyInjection;
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
            // Phase 1 core services
            services.AddScoped<IWatermarkService, WatermarkService>();
            services.AddActionLogger();
            services.AddScoped<IVideoForensicsDataClient, VideoForensicsDataClient>();

            // IntegrityVerificationService implementation (implements IIntegrityVerificationService from Data.Common)
            services.AddScoped<IIntegrityVerificationService, IntegrityVerificationService>();

            // Phase 2 reporting and retention services
            services.AddScoped<IReportGenerationService, ReportGenerationService>();

            // Register RetentionService with configurable retention days
            services.AddScoped<IRetentionService>(sp =>
            {
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RetentionService>>();
                var mediaItemRepo = sp.GetRequiredService<VideoForensics.Data.Common.Contracts.IMediaItemRepository>();
                var legalHoldRepo = sp.GetRequiredService<VideoForensics.Data.Common.Contracts.ILegalHoldRepository>();
                var unitOfWork = sp.GetRequiredService<VideoForensics.Data.Common.Contracts.IUnitOfWork>();
                var actionLogger = sp.GetRequiredService<IActionLogger>();
                var days = retentionDays ?? 90;
                return new RetentionService(mediaItemRepo, legalHoldRepo, unitOfWork, actionLogger, logger, days);
            });

            services.AddScoped<IRedactionService, RedactionService>();

            // Phase 3 reconciliation service
            services.AddScoped<IProviderReconciliationService, ProviderReconciliationService>();

            // Phase 4 export service
            services.AddScoped<IExportRecordService, ExportRecordService>();

            return services;
        }
    }
}
