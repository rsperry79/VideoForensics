using System.Globalization;
using Microsoft.Extensions.Logging;
using VideoForensics.Client.Ui.Shared.Services.Signal;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Hosting.BackgroundServices
{
    /// <summary>
    /// Checks a camera's health history for readings more than two std devs from its all-time mean, and raises one
    /// alert per camera and hour. Runs on the server as part of the base health poll, right after each reading is
    /// recorded, so the calculation and the alert do not depend on any page being open.
    /// </summary>
    public sealed class SignalDeviationAlerts
    {
        /// <summary>How far back a poll looks for flagged hours. Older hours were checked by earlier polls.</summary>
        public static readonly TimeSpan LookBack = TimeSpan.FromHours(24);

        private readonly IDeviceHealthRepository _health;
        private readonly INoticeRepository _notices;
        private readonly INotificationDispatcher _dispatcher;
        private readonly TimeProvider _time;
        private readonly ILogger<SignalDeviationAlerts> _logger;

        public SignalDeviationAlerts(
            IDeviceHealthRepository health,
            INoticeRepository notices,
            INotificationDispatcher dispatcher,
            TimeProvider time,
            ILogger<SignalDeviationAlerts> logger)
        {
            _health = health;
            _notices = notices;
            _dispatcher = dispatcher;
            _time = time;
            _logger = logger;
        }

        /// <summary>Raises an alert for each hour in the look-back window with a reading more than 2 std devs out. Returns how many were raised.</summary>
        public async Task<int> CheckAsync(VideoForensics.Data.Common.Entities.Device device, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(device);

            DateTime now = _time.GetUtcNow().UtcDateTime;
            IReadOnlyList<DeviceHealth> rows = await _health.GetHistoryAsync(device.Id, ct);
            var samples = rows.Select(r => new SignalSample(r.CapturedAtUtc, r.WifiSignalRssi)).ToList();

            // The baseline is the camera's all-time mean and std dev; only the look-back window is checked against it.
            SignalStats baseline = SignalStats.Compute(samples);
            int raised = 0;
            foreach (SignalDeviationWindow window in SignalDeviation
                .Windows(samples, now - LookBack, now, baseline)
                .Where(w => w.Level == SignalDeviationLevel.TwoSigma))
            {
                // The marker is written into the details, so a repeat check of the same hour finds it and stays quiet.
                string marker = SignalDeviation.DedupeKey(device.ProviderName ?? string.Empty, device.ProviderDeviceId, window.HourStartUtc);
                if (await _notices.ExistsWithDetailsAsync(marker, ct))
                {
                    continue;
                }

                // The operator reads the alert on this machine, so the hour is shown in local time. The marker and scope
                // keep UTC: they are identifiers, not display text.
                string hour = DateTime.SpecifyKind(window.HourStartUtc, DateTimeKind.Utc).ToLocalTime()
                    .ToString("ddd MMM d HH:00", CultureInfo.InvariantCulture);

                // The average covers every reading in the hour, not only the flagged ones, so it shows the hour as a whole.
                // The worst reading is kept as well: a short spike is diluted in an hour average, and it is the spike that
                // raised the alert.
                DateTime hourEndUtc = window.HourStartUtc.AddHours(1);
                double hourAverage = samples
                    .Where(s => s.RssiDbm is not null && s.CapturedAtUtc >= window.HourStartUtc && s.CapturedAtUtc < hourEndUtc)
                    .Average(s => s.RssiDbm!.Value);

                // The review link opens the signal history on this camera, drilled to the alerted hour and an hour either side
                // of it, so the operator sees the spike in its context. The bounds are UTC instants the scope query reads.
                string scope = $"devices={device.Id:D}" +
                    $"&from={window.HourStartUtc.AddHours(-1):yyyy-MM-dd'T'HH:mm:ss'Z'}" +
                    $"&to={window.HourStartUtc.AddHours(2):yyyy-MM-dd'T'HH:mm:ss'Z'}";
                await _dispatcher.DispatchAsync(new NotificationEvent(
                    EventType: "SignalDeviation",
                    TimestampUtc: now,
                    OperatorId: null,
                    PairedDeviceId: null,
                    SourceIp: null,
                    Details: $"{device.Name}: hour average RSSI {hourAverage.ToString("0.0", CultureInfo.InvariantCulture)} dBm at {hour} " +
                        $"(worst reading {window.WorstRssiDbm} dBm), camera mean {baseline.Average.ToString("0.0", CultureInfo.InvariantCulture)} dBm. " +
                        $"More than 2 std devs from the mean. [{marker}]",
                    Audience: NotificationAudience.All,
                    Severity: NoticeSeverity.Alert,
                    ActionPath: $"/signal-history?{scope}"), ct);

                _logger.LogInformation("Raised signal deviation alert for {DeviceName} at {HourUtc} UTC", device.Name, window.HourStartUtc);
                raised++;
            }

            return raised;
        }
    }
}
