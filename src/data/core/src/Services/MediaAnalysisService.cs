using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        /// <summary>
        /// Newest clips per device used as history. Baselines are seasonal, so older clips add cost without changing the
        /// judgement; bounding the query keeps each download's analysis cheap as a device's archive grows.
        /// </summary>
        public const int HistoryWindowClips = 500;


        /// <summary>Clips needed before "this lighting mode is unusual for the device" can be judged.</summary>
        private const int UnusualLightingMinimumSamples = 10;

        /// <summary>A mode that is under this share of a device's clips is unusual for it.</summary>
        private const double UnusualLightingShare = 0.05;

        /// <summary>Clips needed before an audio baseline (noise floor, silence) is trusted.</summary>
        private const int AudioBaselineMinimumSamples = 10;

        private readonly IMediaItemRepository _items;
        private readonly IDeviceRepository _devices;
        private readonly Hemisphere _hemisphere;
        private readonly IMediaBitrateAnalyzer _bitrate;
        private readonly IClipColorAnalyzer _color;
        private readonly IAudioClipAnalyzer _audio;
        private readonly ILogger<MediaAnalysisService> _logger;

        /// <summary>Creates the service.</summary>
        public MediaAnalysisService(
            IMediaItemRepository items,
            IDeviceRepository devices,
            IMediaBitrateAnalyzer bitrate,
            IClipColorAnalyzer color,
            IAudioClipAnalyzer audio,
            IOptions<MediaAnalysisOptions> options,
            ILogger<MediaAnalysisService> logger)
        {
            _items = items;
            _devices = devices;
            _hemisphere = options.Value.Hemisphere;
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
            IReadOnlyList<MediaItem> others = (await _items.GetRecentByDeviceAsync(media.DeviceId, HistoryWindowClips, ct))
                .Where(m => m.Id != media.Id)
                .ToList();
            // Clean history is a clip that was analysed and raised no flags; unanalysed clips (null) are not history.
            IReadOnlyList<MediaItem> clean = others.Where(m => m.RedFlags == string.Empty).ToList();

            // Only compare like with like: a stream-reported bitrate is never pooled with an estimated one.
            List<BaselineSample> samples = clean
                .Where(m => m.VideoBitrateBps is not null && ParseMode(m.LightingMode) is not null && m.BitrateEstimated == bitrate?.BitrateEstimated)
                .Select(m => new BaselineSample(m.VideoBitrateBps!.Value, ParseMode(m.LightingMode)!.Value, SeasonExtensions.FromMonth(m.RecordedAtUtc.Month, _hemisphere), Flagged: false))
                .ToList();
            BaselineSnapshot snapshot = SeasonalBaselineBuilder.Build(samples);

            // Drift is a device property, not a clip one: a steady shift is reported for review, never folded into the baseline.
            Device? device = await _devices.GetAsync(media.DeviceId, ct);
            if (device is not null)
            {
                List<long> history = clean
                    .Where(m => m.VideoBitrateBps is not null && m.BitrateEstimated == bitrate?.BitrateEstimated)
                    .OrderBy(m => m.RecordedAtUtc)
                    .Select(m => m.VideoBitrateBps!.Value)
                    .ToList();
                DriftReport drift = DriftDetector.Detect(history);
                device.BaselineDriftDetected = drift.Drifting;
                device.BaselineDriftChangeFraction = drift.ChangeFraction;
                device.BaselineDriftCheckedAtUtc = DateTime.UtcNow;
                await _devices.UpdateAsync(device, ct);
            }

            // Without all three measurements the clip cannot be judged; say so rather than storing "no flags".
            if (bitrate is null || color is null || audio is null)
            {
                _logger.LogWarning("Media {MediaItemId} analysis incomplete (bitrate {HasBitrate}, colour {HasColor}, audio {HasAudio}); red flags not evaluated",
                    media.Id, bitrate is not null, color is not null, audio is not null);
                media.RedFlags = null;
                media.JammingSuspected = null;
                media.BaselineVersion = snapshot.Version;
                await _items.UpdateAsync(media, ct);
                return;
            }

            bool bitrateCollapsed = false;
            if (ParseMode(media.LightingMode) is { } mode)
            {
                BitrateBaseline? baseline = snapshot.Get(mode, SeasonExtensions.FromMonth(media.RecordedAtUtc.Month, _hemisphere));
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
            _logger.LogDebug("Analysed media {MediaItemId}: {FlagCount} red flag(s), jamming suspected: {Jamming}",
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

        /// <summary>Parses a stored lighting mode; unknown or legacy values yield null so one bad row cannot stop analysis.</summary>
        private static LightingMode? ParseMode(string? name) =>
            Enum.TryParse(name, out LightingMode mode) ? mode : null;

        /// <summary>True when this clip's lighting mode is rare among the device's clips.</summary>
        private static bool IsUnusualLighting(string? mode, IReadOnlyList<MediaItem> others)
        {
            if (mode is null)
            {
                return false;
            }

            List<string> modes = others.Where(m => ParseMode(m.LightingMode) is not null).Select(m => m.LightingMode!).ToList();
            if (modes.Count < UnusualLightingMinimumSamples)
            {
                return false;
            }

            double share = (double)modes.Count(m => m == mode) / modes.Count;
            return share < UnusualLightingShare;
        }
    }
}
