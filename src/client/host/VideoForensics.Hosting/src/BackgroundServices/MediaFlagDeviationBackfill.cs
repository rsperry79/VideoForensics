using Microsoft.Extensions.Logging;

using VideoForensics.Client.Ui.Shared.Services.Signal;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Hosting.BackgroundServices
{
    /// <summary>
    /// One-off backfill of <see cref="MediaFlag.DeviationSigma"/> for flags raised before the value was stored. Two passes run,
    /// each with its own marker:
    /// RSSI flags get the sigma of their worst reading against the camera's all-time signal baseline, the same calculation
    /// <see cref="SignalDeviationAlerts"/> stores at raise time. BitrateCollapse flags get the sigma of their clip's bit rate
    /// against the camera's all-time bit rate baseline (<see cref="IMediaItemRepository.GetBitrateBaselineByDeviceAsync"/>).
    /// </summary>
    /// <remarks>
    /// The passes run once: a persisted marker is written when each completes. A failed pass writes no marker, and the next
    /// start retries. Rows that already hold a sigma are never touched, so a repeat is harmless. A collapse flag whose clip
    /// was deleted has nothing to measure, so its sigma stays null.
    /// </remarks>
    public sealed class MediaFlagDeviationBackfill
    {
        /// <summary>App setting that records the completed RSSI pass. Deleting it makes the next start run the pass again.</summary>
        public const string MarkerKey = "MediaFlagDeviationSigmaBackfilled";

        /// <summary>App setting that records the completed bit rate pass. Separate from <see cref="MarkerKey"/> so the RSSI pass is not re-run.</summary>
        public const string BitrateMarkerKey = "MediaFlagBitrateDeviationSigmaBackfilled";

        private readonly IMediaFlagDeviationRepository _flags;
        private readonly IDeviceHealthRepository _health;
        private readonly IMediaItemRepository _media;
        private readonly IAppSettingRepository _settings;
        private readonly ILogger<MediaFlagDeviationBackfill> _logger;

        public MediaFlagDeviationBackfill(
            IMediaFlagDeviationRepository flags,
            IDeviceHealthRepository health,
            IMediaItemRepository media,
            IAppSettingRepository settings,
            ILogger<MediaFlagDeviationBackfill> logger)
        {
            _flags = flags;
            _health = health;
            _media = media;
            _settings = settings;
            _logger = logger;
        }

        /// <summary>Runs each pass unless its marker exists. Returns how many flags received a sigma across both passes (zero when both are skipped).</summary>
        public async Task<int> RunOnceAsync(CancellationToken ct)
        {
            int filled = await RunRssiPassAsync(ct);
            return filled + await RunBitrateCollapsePassAsync(ct);
        }

        private async Task<int> RunRssiPassAsync(CancellationToken ct)
        {
            if (await _settings.GetAsync(MarkerKey, ct) is not null)
            {
                return 0;
            }

            IReadOnlyList<MediaFlag> pending = await _flags.ListWithoutDeviationSigmaAsync(MediaFlagType.RssiDeviation, ct);
            int filled = 0;
            foreach (IGrouping<Guid, MediaFlag> byDevice in pending.GroupBy(f => f.DeviceId))
            {
                ct.ThrowIfCancellationRequested();

                // One history read and one baseline per camera, shared by all of its flags.
                IReadOnlyList<DeviceHealth> rows = await _health.GetHistoryAsync(byDevice.Key, ct);
                var samples = rows.Select(r => new SignalSample(r.CapturedAtUtc, r.WifiSignalRssi)).ToList();
                SignalStats baseline = SignalStats.Compute(samples);

                foreach (MediaFlag flag in byDevice)
                {
                    // The flag's own hour only: a reading in the next hour belongs to that hour's flag.
                    SignalDeviationWindow? window = SignalDeviation.Windows(samples, flag.WindowStartUtc, flag.WindowEndUtc, baseline)
                        .FirstOrDefault(w => w.HourStartUtc == flag.WindowStartUtc);
                    if (window is null || SignalDeviation.DeviationSigma(window.WorstRssiDbm, baseline.Average, baseline.StdDev) is not double sigma)
                    {
                        continue;
                    }

                    await _flags.SetDeviationSigmaAsync(flag.Id, sigma, ct);
                    filled++;
                }
            }

            await _settings.SetAsync(MarkerKey, DateTime.UtcNow.ToString("O"), ct);
            _logger.LogInformation("Deviation backfill stored sigma on {Filled} of {Pending} RSSI flag(s)", filled, pending.Count);
            return filled;
        }

        private async Task<int> RunBitrateCollapsePassAsync(CancellationToken ct)
        {
            if (await _settings.GetAsync(BitrateMarkerKey, ct) is not null)
            {
                return 0;
            }

            IReadOnlyList<MediaFlag> pending = await _flags.ListWithoutDeviationSigmaAsync(MediaFlagType.BitrateCollapse, ct);
            int filled = 0;

            // A flag with no clip has no reading to measure, so it is left out of the device groups.
            foreach (IGrouping<Guid, MediaFlag> byDevice in pending.Where(f => f.MediaItemId is not null).GroupBy(f => f.DeviceId))
            {
                ct.ThrowIfCancellationRequested();

                // One all-time baseline per camera, shared by all of its flags.
                AllTimeBitrateBaseline allTime = await _media.GetBitrateBaselineByDeviceAsync(byDevice.Key, ct);
                foreach (MediaFlag flag in byDevice)
                {
                    MediaItem? clip = await _media.GetAsync(flag.MediaItemId!.Value, ct);
                    if (clip?.VideoBitrateBps is not long bps)
                    {
                        continue;
                    }

                    // The clip's bit rate in kbps, the same rounding and units as BitrateDeviationAlerts, so both sigmas agree.
                    if (SignalDeviation.DeviationSigma(Math.Round(bps / 1000.0), allTime.MeanBps / 1000.0, allTime.StdDevBps / 1000.0) is not double sigma)
                    {
                        continue;
                    }

                    await _flags.SetDeviationSigmaAsync(flag.Id, sigma, ct);
                    filled++;
                }
            }

            await _settings.SetAsync(BitrateMarkerKey, DateTime.UtcNow.ToString("O"), ct);
            _logger.LogInformation("Deviation backfill stored sigma on {Filled} of {Pending} bit rate collapse flag(s)", filled, pending.Count);
            return filled;
        }
    }
}
