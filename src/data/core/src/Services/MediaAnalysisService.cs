using Microsoft.Extensions.Logging;
using VideoForensics.Core.Providers.Core;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Core.Services
{
    /// <summary>Measures a downloaded clip, judges it against its device's own history, and stores the red flags.</summary>
    public interface IMediaAnalysisService
    {
        /// <summary>Analyses one media item and saves the result. A missing item is ignored.</summary>
        Task AnalyzeAsync(Guid mediaItemId, CancellationToken ct);
    }

    /// <inheritdoc />
    public sealed class MediaAnalysisService : IMediaAnalysisService
    {
        /// <summary>Clips needed before "this lighting mode is unusual for the device" can be judged.</summary>
        private const int UnusualLightingMinimumSamples = 10;

        /// <summary>A mode that is under this share of a device's clips is unusual for it.</summary>
        private const double UnusualLightingShare = 0.05;

        /// <summary>Clips needed before an audio baseline (noise floor, silence) is trusted.</summary>
        private const int AudioBaselineMinimumSamples = 10;

        private readonly IMediaItemRepository _items;
        private readonly IMediaBitrateAnalyzer _bitrate;
        private readonly IClipColorAnalyzer _color;
        private readonly IAudioClipAnalyzer _audio;
        private readonly ILogger<MediaAnalysisService> _logger;

        /// <summary>Creates the service.</summary>
        public MediaAnalysisService(
            IMediaItemRepository items,
            IMediaBitrateAnalyzer bitrate,
            IClipColorAnalyzer color,
            IAudioClipAnalyzer audio,
            ILogger<MediaAnalysisService> logger)
        {
            _items = items;
            _bitrate = bitrate;
            _color = color;
            _audio = audio;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task AnalyzeAsync(Guid mediaItemId, CancellationToken ct)
        {
            MediaItem? media = await _items.GetAsync(mediaItemId, ct);
            if (media is null)
            {
                return;
            }

            MediaBitrateAnalysis? bitrate = await _bitrate.AnalyzeAsync(media.FilePath, media.FileSizeBytes, ct);
            ClipColorStats? color = await _color.AnalyzeAsync(media.FilePath, ct);
            AudioClipStats? audio = await _audio.AnalyzeAsync(media.FilePath, bitrate?.DurationSeconds, ct);

            if (bitrate is not null)
            {
                media.VideoBitrateBps = bitrate.VideoBitrateBps;
                media.DurationSeconds = bitrate.DurationSeconds;
                media.BitrateEstimated = bitrate.BitrateEstimated;
                media.BitrateAnalyzedAtUtc = DateTime.UtcNow;
            }

            if (color is not null)
            {
                media.LightingMode = LightingClassifier.Classify(color).ToString();
                media.MeanSaturation = color.MeanSaturation;
                media.MeanLuma = color.MeanLuma;
            }

            if (audio is not null)
            {
                media.ClippedPeakCount = audio.ClippedPeakCount;
                media.NoiseFloorDb = audio.NoiseFloorDb;
                media.SilentGapCount = audio.SilentGapCount;
                media.SilenceFraction = audio.SilenceFraction;
            }

            // Judge against the device's own earlier clips. Flagged clips are excluded from every baseline.
            IReadOnlyList<MediaItem> others = (await _items.GetByDeviceIdAsync(media.DeviceId, ct))
                .Where(m => m.Id != media.Id)
                .ToList();
            IReadOnlyList<MediaItem> clean = others.Where(m => string.IsNullOrEmpty(m.RedFlags)).ToList();

            List<BaselineSample> samples = clean
                .Where(m => m.VideoBitrateBps is not null && m.LightingMode is not null)
                .Select(m => new BaselineSample(m.VideoBitrateBps!.Value, Enum.Parse<LightingMode>(m.LightingMode!), SeasonExtensions.FromMonth(m.RecordedAtUtc.Month), Flagged: false))
                .ToList();
            BaselineSnapshot snapshot = SeasonalBaselineBuilder.Build(samples.Count, samples);

            bool bitrateCollapsed = false;
            if (bitrate is not null && media.LightingMode is { } modeName)
            {
                BitrateBaseline? baseline = snapshot.Get(Enum.Parse<LightingMode>(modeName), SeasonExtensions.FromMonth(media.RecordedAtUtc.Month));
                bitrateCollapsed = InterferenceAssessor.Assess(bitrate.VideoBitrateBps, baseline).InterferenceSuspected;
            }

            double? noiseRise = audio?.NoiseFloorDb is { } noise
                ? RiseAboveMedian(noise, clean.Where(m => m.NoiseFloorDb is not null).Select(m => m.NoiseFloorDb!.Value))
                : null;
            double? silenceRise = audio?.SilenceFraction is { } silence
                ? RiseAboveMedian(silence, clean.Where(m => m.SilenceFraction is not null).Select(m => m.SilenceFraction!.Value))
                : null;

            bool unusualLighting = IsUnusualLighting(media.LightingMode, others);

            RedFlagReport report = RedFlagEvaluator.Evaluate(new ClipSignals(
                BitrateCollapsed: bitrateCollapsed,
                ClippedPeakCount: audio?.ClippedPeakCount ?? 0,
                NoiseFloorRiseDb: noiseRise,
                UnusualLighting: unusualLighting,
                SilentGapCount: audio?.SilentGapCount ?? 0,
                SilenceAboveBaselineFraction: silenceRise));

            media.RedFlags = string.Join(",", report.Flags);
            media.JammingSuspected = report.JammingSuspected;
            media.BaselineVersion = snapshot.Version;

            await _items.UpdateAsync(media, ct);
            _logger.LogInformation("Analysed media {MediaItemId}: {FlagCount} red flag(s), jamming suspected: {Jamming}",
                media.Id, report.Flags.Count, report.JammingSuspected);
        }

        /// <summary>How far a value sits above the median of the device's clean history; null when there is too little history.</summary>
        private static double? RiseAboveMedian(double value, IEnumerable<double> history)
        {
            List<double> sorted = history.OrderBy(v => v).ToList();
            if (sorted.Count < AudioBaselineMinimumSamples)
            {
                return null;
            }

            int mid = sorted.Count / 2;
            double median = sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
            return value - median;
        }

        /// <summary>True when this clip's lighting mode is rare among the device's clips.</summary>
        private static bool IsUnusualLighting(string? mode, IReadOnlyList<MediaItem> others)
        {
            if (mode is null)
            {
                return false;
            }

            List<string> modes = others.Where(m => m.LightingMode is not null).Select(m => m.LightingMode!).ToList();
            if (modes.Count < UnusualLightingMinimumSamples)
            {
                return false;
            }

            double share = (double)modes.Count(m => m == mode) / modes.Count;
            return share < UnusualLightingShare;
        }
    }
}
