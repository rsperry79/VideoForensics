using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Client.Ui.Shared.Services.Signal;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Hosting.Signal
{
    /// <summary>
    /// Computes the bit rate figures on the server from each camera's measured clip bitrates. Only stream-reported values
    /// count: the query leaves out estimated and missing ones. Readings are fed to the shared signal maths as kbps.
    /// </summary>
    public sealed class BitrateAnalysisService(IDeviceRepository devices, IMediaItemRepository mediaItems) : IBitrateAnalysisService
    {
        /// <summary>Red-flag token <c>MediaAnalysisService</c> stores (the <c>RedFlag.BitrateCollapse</c> name) when bit rate collapsed.</summary>
        private const string CollapseFlag = "BitrateCollapse";

        private readonly IDeviceRepository _devices = devices;
        private readonly IMediaItemRepository _mediaItems = mediaItems;

        /// <inheritdoc />
        public async Task<BitrateAnalysis> AnalyzeAsync(IReadOnlyList<Guid> deviceIds, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(deviceIds);
            if (fromUtc > toUtc)
            {
                throw new ArgumentException("The window start must not be after its end.", nameof(fromUtc));
            }

            // Ids the caller asked for that do not exist are simply not in the result.
            var wanted = new HashSet<Guid>(deviceIds);
            List<Device> found = [.. (await _devices.ListAsync(ct))
                .Where(d => wanted.Contains(d.Id))
                .OrderBy(d => d.Name)];

            var analyses = new List<BitrateDeviceAnalysis>(found.Count);
            foreach (Device device in found)
            {
                // The query already dropped estimated and unmeasured clips and ordered oldest first. The whole history is
                // needed for the all-time baseline, so the window is cut from it here rather than in SQL.
                IReadOnlyList<MeasuredBitrate> rows = await _mediaItems.GetMeasuredBitrateByDeviceAsync(device.Id, ct);
                List<BitrateSample> history = [.. rows.Select(r => new BitrateSample(
                    r.RecordedAtUtc,
                    (int)Math.Round(r.VideoBitrateBps / 1000.0),
                    r.LightingMode,
                    HasCollapseFlag(r.RedFlags)))];

                List<SignalSample> samples = [.. history.Select(h => h.ToSignalSample())];
                var allTime = SignalStats.Compute(samples);

                List<BitrateSample> inWindowReadings = [.. history.Where(h => h.CapturedAtUtc >= fromUtc && h.CapturedAtUtc <= toUtc)];
                List<SignalSample> inWindow = [.. inWindowReadings.Select(h => h.ToSignalSample())];

                analyses.Add(new BitrateDeviceAnalysis(
                    device.Id,
                    DisplayName(device),
                    device.ProviderName,
                    device.ProviderDeviceId,
                    allTime,
                    SignalStats.Compute(inWindow),
                    inWindowReadings,
                    SignalDeviation.Windows(samples, fromUtc, toUtc, allTime),
                    SignalCalendar.Compute(samples, fromUtc, toUtc),
                    SignalBuckets.Compute(inWindow, fromUtc, toUtc, SignalBucket.Hour),
                    SignalBuckets.Compute(inWindow, fromUtc, toUtc, SignalBucket.Day),
                    SignalBuckets.Compute(inWindow, fromUtc, toUtc, SignalBucket.Week)));
            }

            return new BitrateAnalysis(analyses);
        }

        private static bool HasCollapseFlag(string? redFlags)
        {
            return redFlags is not null
                && redFlags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Contains(CollapseFlag, StringComparer.Ordinal);
        }

        private static string DisplayName(Device device)
        {
            return string.IsNullOrWhiteSpace(device.Name) ? DeviceLabel.For(device.Name, device.ProviderDeviceId) : device.Name;
        }
    }
}
