using VideoForensics.Core.Api.Contracts.Repositories;
using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Client.Ui.Shared.Services.Signal;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Ui.Shared.Services.Review;

/// <summary>
/// Reads the Detail tab's data through the repositories and the signal service. Event detail is batched so one
/// request never names more than the repository accepts.
/// </summary>
public sealed class ReviewDetailLoader : IReviewDetailLoader
{
    /// <summary>Most event ids one detail query names. The repository rejects more.</summary>
    public const int DetailChunkSize = 200;

    private readonly IEventDetailRepository _details;
    private readonly IJammingRepository _jamming;
    private readonly ISignalAnalysisService _signals;
    private readonly IMediaFlagRepository _flags;

    /// <summary>Creates the loader over the detail, jamming and flag repositories and the signal analysis service.</summary>
    public ReviewDetailLoader(IEventDetailRepository details, IJammingRepository jamming, ISignalAnalysisService signals, IMediaFlagRepository flags)
    {
        _details = details;
        _jamming = jamming;
        _signals = signals;
        _flags = flags;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, EventDetail>> LoadEventDetailsAsync(IReadOnlyCollection<Guid> eventIds, CancellationToken ct)
    {
        var result = new Dictionary<Guid, EventDetail>();
        var distinct = eventIds.Distinct().ToList();
        for (int start = 0; start < distinct.Count; start += DetailChunkSize)
        {
            List<Guid> chunk = distinct.GetRange(start, Math.Min(DetailChunkSize, distinct.Count - start));
            IReadOnlyList<EventDetail> rows = await _details.GetDetailsAsync(chunk, ct);
            foreach (EventDetail row in rows)
            {
                result[row.EventId] = row;
            }
        }

        return result;
    }

    /// <inheritdoc />
    /// <remarks>The repository filters one camera at a time, so each camera is one query and the results are merged here.</remarks>
    public async Task<IReadOnlyList<JammingIncidentRecord>> LoadJammingAsync(IReadOnlyList<Guid> deviceIds, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        var merged = new List<JammingIncidentRecord>();
        foreach (Guid device in deviceIds.Distinct())
        {
            IReadOnlyList<JammingIncidentRecord> rows = await _jamming.ListIncidentsAsync(device, fromUtc, toUtc, ct);
            merged.AddRange(rows);
        }

        return merged.OrderByDescending(incident => incident.StartUtc).ToList();
    }

    /// <inheritdoc />
    /// <remarks>
    /// The signal service flags one hour at a time. A normal reading is not notable, so only flagged hours become anomalies.
    /// </remarks>
    public async Task<IReadOnlyList<ReviewAnomaly>> LoadAnomaliesAsync(IReadOnlyList<Guid> deviceIds, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        var devices = deviceIds.Distinct().ToList();
        if (devices.Count == 0)
        {
            return [];
        }

        SignalAnalysis analysis = await _signals.AnalyzeAsync(devices, fromUtc, toUtc, ct);
        return analysis.Devices
            .SelectMany(device => device.Deviations
                .Where(window => window.Level != SignalDeviationLevel.None)
                .Select(window => new ReviewAnomaly(device.DeviceId, window.HourStartUtc, window.HourStartUtc.AddHours(1), window.Level)))
            .OrderByDescending(anomaly => anomaly.StartUtc)
            .ToList();
    }

    /// <inheritdoc />
    /// <remarks>Reads the bit-rate flags over the cameras and range, newest first. Each page is one flag query.</remarks>
    public async Task<IReadOnlyList<ReviewBitRateFlag>> LoadBitRateFlagsAsync(IReadOnlyList<Guid> deviceIds, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        var devices = deviceIds.Distinct().ToList();
        if (devices.Count == 0)
        {
            return [];
        }

        var filter = new MediaFlagFilterDto(["BitrateCollapse", "BitrateDeviation"], null, devices, fromUtc, toUtc);
        MediaFlagPageDto page = await _flags.ListPageAsync(filter, 0, BitRateFlagPageSize, ct);
        return page.Items
            .OrderByDescending(flag => flag.WindowStartUtc)
            .Select(flag => new ReviewBitRateFlag(flag.Id, flag.DeviceId, flag.WindowStartUtc, flag.WindowEndUtc, flag.FlagType, flag.MediaItemId))
            .ToList();
    }

    /// <summary>Most bit-rate flags the Detail section reads. The section shows the newest of them.</summary>
    private const int BitRateFlagPageSize = 500;
}
