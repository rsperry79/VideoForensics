using Microsoft.Extensions.Logging;

using System.Globalization;

using VideoForensics.Client.Ui.Shared.Services.Signal;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Hosting.BackgroundServices
{
    /// <summary>
    /// Checks a camera's measured clip bit rates for readings more than two std devs from its all-time mean, and raises
    /// one alert per camera and hour. Runs on the server as part of the base health poll, right after each reading is
    /// recorded, so the calculation and the alert do not depend on any page being open.
    /// Each raised alert also writes a BitrateDeviation <see cref="MediaFlag"/> for review, linked to the alert's notice.
    /// A repeat check of the same hour finds the notice marker and writes neither notice nor flag.
    /// </summary>
    public sealed class BitrateDeviationAlerts
    {
        /// <summary>How far back a poll looks for flagged hours. Older hours were checked by earlier polls.</summary>
        public static readonly TimeSpan LookBack = TimeSpan.FromHours(24);

        private readonly IMediaItemWriteRepository _mediaItems;
        private readonly INoticeRepository _notices;
        private readonly IMediaFlagWriteRepository _flags;
        private readonly INotificationDispatcher _dispatcher;
        private readonly TimeProvider _time;
        private readonly ILogger<BitrateDeviationAlerts> _logger;

        public BitrateDeviationAlerts(
            IMediaItemWriteRepository mediaItems,
            INoticeRepository notices,
            IMediaFlagWriteRepository flags,
            INotificationDispatcher dispatcher,
            TimeProvider time,
            ILogger<BitrateDeviationAlerts> logger)
        {
            _mediaItems = mediaItems;
            _notices = notices;
            _flags = flags;
            _dispatcher = dispatcher;
            _time = time;
            _logger = logger;
        }

        /// <summary>Raises an alert for each hour in the look-back window with a clip bit rate more than 2 std devs out. Returns how many were raised.</summary>
        public async Task<int> CheckAsync(VideoForensics.Data.Common.Entities.Device device, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(device);

            DateTime now = _time.GetUtcNow().UtcDateTime;
            IReadOnlyList<MeasuredBitrate> rows = await _mediaItems.GetMeasuredBitrateByDeviceAsync(device.Id, ct);

            // The same kbps conversion as BitrateAnalysisService, so the alert and the bit rate page agree on every value.
            var samples = rows
                .Select(r => new BitrateSample(r.RecordedAtUtc, (int)Math.Round(r.VideoBitrateBps / 1000.0), r.LightingMode, Collapsed: false).ToSignalSample())
                .ToList();

            // The baseline is the camera's all-time mean and std dev; only the look-back window is checked against it.
            var baseline = SignalStats.Compute(samples);

            // The sigma stored on each flag comes from the server's all-time bit rate baseline (an SQL aggregate). It is in bps,
            // so it is divided to kbps to match the worst reading, which is kbps like the samples.
            AllTimeBitrateBaseline allTime = await _mediaItems.GetBitrateBaselineByDeviceAsync(device.Id, ct);
            int raised = 0;
            foreach (SignalDeviationWindow window in SignalDeviation
                .Windows(samples, now - LookBack, now, baseline)
                .Where(w => w.Level == SignalDeviationLevel.TwoSigma))
            {
                // The provider prefix keeps this marker apart from the RSSI marker for the same camera and hour.
                string marker = SignalDeviation.DedupeKey("bitrate:" + (device.ProviderName ?? string.Empty), device.ProviderDeviceId, window.HourStartUtc);
                if (await _notices.ExistsWithDetailsAsync(marker, ct))
                {
                    continue;
                }

                // The operator reads the alert on this machine, so the hour is shown in local time. The marker and scope
                // keep UTC: they are identifiers, not display text.
                string hour = DateTime.SpecifyKind(window.HourStartUtc, DateTimeKind.Utc).ToLocalTime()
                    .ToString("ddd MMM d HH:00", CultureInfo.InvariantCulture);

                // The average covers every clip in the hour, not only the flagged ones, so it shows the hour as a whole.
                // The worst clip is kept as well: a short dip is diluted in an hour average, and it is the dip that raised the alert.
                DateTime hourEndUtc = window.HourStartUtc.AddHours(1);
                double hourAverage = samples
                    .Where(s => s.RssiDbm is not null && s.CapturedAtUtc >= window.HourStartUtc && s.CapturedAtUtc < hourEndUtc)
                    .Average(s => s.RssiDbm!.Value);

                // The review link opens the bit rate history on this camera, drilled to the alerted hour and an hour either side
                // of it, so the operator sees the change in its context. The bounds are UTC instants the scope query reads.
                string scope = $"devices={device.Id:D}" +
                    $"&from={window.HourStartUtc.AddHours(-1):yyyy-MM-dd'T'HH:mm:ss'Z'}" +
                    $"&to={window.HourStartUtc.AddHours(2):yyyy-MM-dd'T'HH:mm:ss'Z'}";
                // The notice id is fixed here so the review flag can point at the notice the dispatcher writes.
                var noticeId = Guid.NewGuid();

                // The flag is written before the dispatch. If the dispatch throws, the marker is not in the notice table,
                // so the next poll retries this hour, and the flag upsert just refreshes the same row. The hour is the flag
                // window, and the flag has no clip, so MediaItemId stays null.
                _ = await _flags.UpsertRaisedAsync(new MediaFlag
                {
                    Id = Guid.NewGuid(),
                    FlagType = MediaFlagType.BitrateDeviation,
                    DeviceId = device.Id,
                    MediaItemId = null,
                    WindowStartUtc = window.HourStartUtc,
                    WindowEndUtc = hourEndUtc,
                    RaisedAtUtc = now,
                    Severity = MediaFlagSeverity.Alert,
                    NoticeId = noticeId,
                    DeviationSigma = SignalDeviation.DeviationSigma(window.WorstRssiDbm, allTime.MeanBps / 1000.0, allTime.StdDevBps / 1000.0)
                }, ct);

                await _dispatcher.DispatchAsync(new NotificationEvent(
                    EventType: "BitrateDeviation",
                    TimestampUtc: now,
                    OperatorId: null,
                    PairedDeviceId: null,
                    SourceIp: null,
                    Details: $"{device.Name}: {hourAverage.ToString("0", CultureInfo.InvariantCulture)} kbps at {hour} " +
                        $"(worst reading {window.WorstRssiDbm} kbps), camera mean {baseline.Average.ToString("0", CultureInfo.InvariantCulture)} kbps. " +
                        $"More than 2 std devs from the mean. [{marker}]",
                    Audience: NotificationAudience.All,
                    Severity: NoticeSeverity.Alert,
                    ActionPath: $"/bitrate-history?{scope}",
                    NoticeId: noticeId), ct);

                _logger.LogInformation("Raised bit rate deviation alert for {DeviceName} at {HourUtc} UTC", device.Name, window.HourStartUtc);
                raised++;
            }

            return raised;
        }
    }
}
