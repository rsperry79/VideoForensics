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
            // Holder for the configured ffmpeg bin directory; ForensicsConfigurationService writes it, tool consumers read it.
            services.TryAddSingleton<VideoForensics.Core.Providers.Common.Contracts.IFfmpegToolLocation, VideoForensics.Core.Providers.Core.FfmpegToolLocation>();
            // Transient: the bin directory may be set after first resolve, so each resolve must read it fresh.
            services.TryAddTransient<VideoForensics.Core.Providers.Core.IMediaBitrateAnalyzer>(sp =>
                new VideoForensics.Core.Providers.Core.FfprobeMediaBitrateAnalyzer(
                    VideoForensics.Core.Providers.Core.FfmpegPathResolver.Resolve(null, "ffprobe", sp.GetRequiredService<VideoForensics.Core.Providers.Common.Contracts.IFfmpegToolLocation>().BinDirectory)));
            // Transient for the same reason as IMediaBitrateAnalyzer: ffmpeg path reads the bin directory per resolve.
            services.TryAddTransient<VideoForensics.Core.Providers.Core.IClipColorAnalyzer>(sp =>
                new VideoForensics.Core.Providers.Core.FfmpegClipColorAnalyzer(
                    VideoForensics.Core.Providers.Core.FfmpegPathResolver.Resolve(null, "ffmpeg", sp.GetRequiredService<VideoForensics.Core.Providers.Common.Contracts.IFfmpegToolLocation>().BinDirectory)));
            services.TryAddTransient<VideoForensics.Core.Providers.Core.IAudioClipAnalyzer>(sp =>
                new VideoForensics.Core.Providers.Core.FfmpegAudioAnalyzer(
                    VideoForensics.Core.Providers.Core.FfmpegPathResolver.Resolve(null, "ffmpeg", sp.GetRequiredService<VideoForensics.Core.Providers.Common.Contracts.IFfmpegToolLocation>().BinDirectory)));
            // Transient for the same reason: ffmpeg path reads the bin directory per resolve.
            services.TryAddTransient<VideoForensics.Core.Providers.Common.Contracts.ITimelineVideoBuilder>(sp =>
                new VideoForensics.Core.Providers.Core.FfmpegTimelineVideoBuilder(
                    sp.GetRequiredService<ILogger<VideoForensics.Core.Providers.Core.FfmpegTimelineVideoBuilder>>(),
                    VideoForensics.Core.Providers.Core.FfmpegPathResolver.Resolve(null, "ffmpeg", sp.GetRequiredService<VideoForensics.Core.Providers.Common.Contracts.IFfmpegToolLocation>().BinDirectory)));
            _ = services.AddOptions<VideoForensics.Data.Core.Services.MediaAnalysisOptions>();
            // Snapshot timelines: the mapper has no dependencies; the service reads stored captures and drives the builder above.
            services.TryAddSingleton<VideoForensics.Core.Providers.Common.Contracts.ISnapshotStillMapper, VideoForensics.Core.Providers.Core.SnapshotStillMapper>();
            services.TryAddScoped<VideoForensics.Data.Core.Contracts.ISnapshotTimelineService, VideoForensics.Data.Core.Services.SnapshotTimelineService>();
            services.TryAddSingleton<VideoForensics.Data.Core.Contracts.IMediaThumbnailNotifier, VideoForensics.Data.Core.Services.NullMediaThumbnailNotifier>();
            services.TryAddSingleton<VideoForensics.Data.Core.Contracts.IEvidenceChangeNotifier, VideoForensics.Data.Core.Services.NullEvidenceChangeNotifier>();
            services.TryAddSingleton<VideoForensics.Data.Core.Contracts.IDeviceHealthChangeNotifier, VideoForensics.Data.Core.Services.NullDeviceHealthChangeNotifier>();
            services.TryAddScoped<VideoForensics.Data.Core.Contracts.IMediaThumbnailService, VideoForensics.Data.Core.Services.MediaThumbnailService>();
            services.TryAddScoped<VideoForensics.Data.Core.Contracts.IMediaPreviewService, VideoForensics.Data.Core.Services.MediaPreviewService>();
            services.TryAddScoped<VideoForensics.Data.Core.Contracts.IMediaThumbnailBackfill, VideoForensics.Data.Core.Services.MediaThumbnailBackfill>();
            services.TryAddScoped<VideoForensics.Data.Core.Contracts.IMediaPreviewBackfill, VideoForensics.Data.Core.Services.MediaPreviewBackfill>();
            services.TryAddScoped<VideoForensics.Data.Core.Contracts.IMediaAnalysisService, VideoForensics.Data.Core.Services.MediaAnalysisService>();
            services.TryAddScoped<VideoForensics.Data.Core.Contracts.IJammingAnalysisBackfill, VideoForensics.Data.Core.Services.JammingAnalysisBackfill>();
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
                return new RetentionService(mediaItemRepo, legalHoldRepo, unitOfWork, actionLogger, logger, days, sp.GetService<IMediaPathResolver>());
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
