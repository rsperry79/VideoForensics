using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Client.Ui.Shared.Services.Scope;
using VideoForensics.Client.Ui.Shared.Services.Signal;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Hosting.Signal
{
    /// <summary>
    /// Computes the signal figures on the server from each camera's health history. The expected reading spacing is the
    /// account's real capture spacing, so the missed-check overlay matches what the poll actually ran at.
    /// </summary>
    public sealed class SignalAnalysisService : ISignalAnalysisService
    {
        private readonly IDeviceRepository _devices;
        private readonly IDeviceHealthRepository _health;
        private readonly ISyncScheduleRepository _schedules;
        private readonly DeviceOwnershipService _ownership;

        public SignalAnalysisService(
            IDeviceRepository devices,
            IDeviceHealthRepository health,
            ISyncScheduleRepository schedules,
            DeviceOwnershipService ownership)
        {
            _devices = devices;
            _health = health;
            _schedules = schedules;
            _ownership = ownership;
        }

        /// <inheritdoc />
        public async Task<SignalAnalysis> AnalyzeAsync(IReadOnlyList<Guid> deviceIds, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(deviceIds);
            if (fromUtc > toUtc)
            {
                throw new ArgumentException("The window start must not be after its end.", nameof(fromUtc));
            }

            // Ids the caller asked for that do not exist are simply not in the result.
            var wanted = new HashSet<Guid>(deviceIds);
            List<Device> devices = (await _devices.ListAsync(ct))
                .Where(d => wanted.Contains(d.Id))
                .OrderBy(d => d.Name)
                .ToList();

            DeviceOwnershipSnapshot ownership = await _ownership.LoadAsync(devices, ct);
            var schedulesByAccount = new Dictionary<Guid, SyncSchedule?>();

            var analyses = new List<SignalDeviceAnalysis>(devices.Count);
            foreach (Device device in devices)
            {
                Func<DateTime, TimeSpan> expected = await ExpectedIntervalAsync(device.Id, ownership.Ownership, schedulesByAccount, ct);

                // The whole history gives the baseline and the coverage of the first readings in the window.
                IReadOnlyList<DeviceHealth> rows = await _health.GetHistoryAsync(device.Id, ct);
                var samples = rows.Select(r => new SignalSample(r.CapturedAtUtc, r.WifiSignalRssi)).ToList();
                SignalStats allTime = SignalStats.Compute(samples);

                List<SignalSample> inWindow = samples
                    .Where(s => s.CapturedAtUtc >= fromUtc && s.CapturedAtUtc <= toUtc)
                    .OrderBy(s => s.CapturedAtUtc)
                    .ToList();

                analyses.Add(new SignalDeviceAnalysis(
                    device.Id,
                    DisplayName(device),
                    device.ProviderName,
                    device.ProviderDeviceId,
                    allTime,
                    SignalStats.Compute(inWindow),
                    inWindow,
                    SignalChartModel.Periods(samples, fromUtc, toUtc, expected),
                    SignalDeviation.Windows(samples, fromUtc, toUtc, allTime),
                    SignalCalendar.Compute(samples, fromUtc, toUtc),
                    SignalBuckets.Compute(inWindow, fromUtc, toUtc, SignalBucket.Hour),
                    SignalBuckets.Compute(inWindow, fromUtc, toUtc, SignalBucket.Day),
                    SignalBuckets.Compute(inWindow, fromUtc, toUtc, SignalBucket.Week)));
            }

            return new SignalAnalysis(analyses);
        }

        /// <summary>
        /// The camera's expected reading spacing at a point in time: the spacing its account actually captures at, which
        /// narrows inside jamming windows. Accounts are looked up once per call. A camera with no account or schedule
        /// uses the default interval.
        /// </summary>
        private async Task<Func<DateTime, TimeSpan>> ExpectedIntervalAsync(
            Guid deviceId,
            IReadOnlyList<DeviceOwnership> ownership,
            Dictionary<Guid, SyncSchedule?> schedulesByAccount,
            CancellationToken ct)
        {
            DeviceOwnership? owner = ownership.FirstOrDefault(o => o.DeviceId == deviceId);
            if (owner is null)
            {
                return _ => SignalChartModel.ExpectedIntervalFor(null);
            }

            if (!schedulesByAccount.TryGetValue(owner.ProviderAccountId, out SyncSchedule? schedule))
            {
                schedule = await _schedules.GetByProviderAccountIdAsync(owner.ProviderAccountId, ct);
                schedulesByAccount[owner.ProviderAccountId] = schedule;
            }

            if (schedule is null)
            {
                return _ => SignalChartModel.ExpectedIntervalFor(null);
            }

            // Same rule the capture service runs at, so the chart and the captures agree.
            return utc => TimeSpan.FromSeconds(SnapshotIntervals.CaptureSpacing(schedule, utc));
        }

        private static string DisplayName(Device device)
        {
            return string.IsNullOrWhiteSpace(device.Name) ? DeviceLabel.For(device.Name, device.ProviderDeviceId) : device.Name;
        }
    }
}
