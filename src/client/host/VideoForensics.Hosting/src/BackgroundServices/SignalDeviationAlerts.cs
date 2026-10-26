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

                string hour = window.HourStartUtc.ToString("ddd MMM d HH:00", CultureInfo.InvariantCulture);

                // The action path carries the camera and day in the scope query the signal page and case form read, so
                // the notice can open that scope or create a case for it.
                string scope = $"devices={device.Id:D}&from={window.HourStartUtc:yyyy-MM-dd}&to={window.HourStartUtc.AddDays(1):yyyy-MM-dd}";
                await _dispatcher.DispatchAsync(new NotificationEvent(
                    EventType: "SignalDeviation",
                    TimestampUtc: now,
                    OperatorId: null,
                    PairedDeviceId: null,
                    SourceIp: null,
                    Details: $"{device.Name}: RSSI {window.WorstRssiDbm} dBm at {hour} UTC is more than 2 std devs from this camera's mean. [{marker}]",
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
