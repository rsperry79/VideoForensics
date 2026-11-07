using Microsoft.Extensions.Logging;

using VideoForensics.Client.Ui.Shared.Services.Signal;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Hosting.BackgroundServices
{
    /// <summary>
    /// One-off backfill of <see cref="MediaFlag.DeviationSigma"/> for RSSI flags raised before the value was stored. Each
    /// flag gets the sigma of its worst reading against the camera's all-time signal baseline, the same calculation
    /// <see cref="SignalDeviationAlerts"/> stores at raise time.
    /// </summary>
    /// <remarks>
    /// Bit rate collapse flags are not covered: there is no camera-wide all-time bit rate baseline yet (the bucketed
    /// baseline is hour-of-day specific), so their sigma stays null until that baseline exists.
    /// The pass runs once: a persisted marker is written when it completes. A failed pass writes no marker, and the next
    /// start retries. Rows that already hold a sigma are never touched, so a repeat is harmless.
    /// </remarks>
    public sealed class MediaFlagDeviationBackfill
    {
        /// <summary>App setting that records the completed pass. Deleting it makes the next start run the pass again.</summary>
        public const string MarkerKey = "MediaFlagDeviationSigmaBackfilled";

        private readonly IMediaFlagDeviationRepository _flags;
        private readonly IDeviceHealthRepository _health;
        private readonly IAppSettingRepository _settings;
        private readonly ILogger<MediaFlagDeviationBackfill> _logger;

        public MediaFlagDeviationBackfill(
            IMediaFlagDeviationRepository flags,
            IDeviceHealthRepository health,
            IAppSettingRepository settings,
            ILogger<MediaFlagDeviationBackfill> logger)
        {
            _flags = flags;
            _health = health;
            _settings = settings;
            _logger = logger;
        }

        /// <summary>Runs the pass unless its marker exists. Returns how many flags received a sigma (zero when skipped).</summary>
        public async Task<int> RunOnceAsync(CancellationToken ct)
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
    }
}
