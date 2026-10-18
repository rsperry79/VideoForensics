using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using VideoForensics.Client.Common.Contracts;
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
            _ = services.AddScoped<IWatermarkService, WatermarkService>();
            _ = services.AddActionLogger();
            services.TryAddSingleton<VideoForensics.Core.Providers.Core.IMediaBitrateAnalyzer, VideoForensics.Core.Providers.Core.FfprobeMediaBitrateAnalyzer>();
            services.TryAddSingleton<VideoForensics.Core.Providers.Core.IClipColorAnalyzer, VideoForensics.Core.Providers.Core.FfmpegClipColorAnalyzer>();
            services.TryAddSingleton<VideoForensics.Core.Providers.Core.IAudioClipAnalyzer, VideoForensics.Core.Providers.Core.FfmpegAudioAnalyzer>();
            services.AddOptions<VideoForensics.Data.Core.Services.MediaAnalysisOptions>();
            services.TryAddScoped<VideoForensics.Data.Core.Services.IMediaAnalysisService, VideoForensics.Data.Core.Services.MediaAnalysisService>();
            _ = services.AddScoped<IVideoForensicsDataClient, VideoForensicsDataClient>();

            // IntegrityVerificationService implementation (implements IIntegrityVerificationService from Data.Common)
            _ = services.AddScoped<IIntegrityVerificationService, IntegrityVerificationService>();

            // Phase 2 reporting and retention services
            _ = services.AddScoped<IReportGenerationService, ReportGenerationService>();
            _ = services.AddScoped<IStorageSettingsService, StorageSettingsService>();

            // Register RetentionService with configurable retention days
            _ = services.AddScoped<IRetentionService>(sp =>
            {
                ILogger<RetentionService> logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RetentionService>>();
                IMediaItemRepository mediaItemRepo = sp.GetRequiredService<VideoForensics.Data.Common.Contracts.IMediaItemRepository>();
                ILegalHoldRepository legalHoldRepo = sp.GetRequiredService<VideoForensics.Data.Common.Contracts.ILegalHoldRepository>();
                IUnitOfWork unitOfWork = sp.GetRequiredService<VideoForensics.Data.Common.Contracts.IUnitOfWork>();
                IActionLogger actionLogger = sp.GetRequiredService<IActionLogger>();
                int days = retentionDays ?? 90;
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
