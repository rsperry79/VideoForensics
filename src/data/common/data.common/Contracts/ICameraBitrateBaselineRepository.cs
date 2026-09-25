using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for time-bucketed camera bitrate baselines used in interference detection.</summary>
    public interface ICameraBitrateBaselineRepository
    {
        /// <summary>Gets the baseline statistics for a specific hour/weekend bucket of a device.</summary>
        Task<CameraBitrateBaseline?> GetBucketAsync(Guid deviceId, int hourOfDay, bool isWeekend, CancellationToken ct);

        /// <summary>Gets the device-global fallback baseline (average across all buckets) for cold-start scenarios.</summary>
        Task<CameraBitrateBaseline?> GetDeviceGlobalAsync(Guid deviceId, CancellationToken ct);

        /// <summary>Recomputes and upserts the baseline statistics for a specific hour/weekend bucket from current telemetry.</summary>
        Task RecomputeBucketAsync(Guid deviceId, int hourOfDay, bool isWeekend, CancellationToken ct);

        /// <summary>Lists all buckets meeting calibration criteria (undersample or stale).</summary>
        Task<IReadOnlyList<(Guid DeviceId, int HourOfDay, bool IsWeekend)>> ListBucketsNeedingCalibrationAsync(int minSamples, TimeSpan staleness, CancellationToken ct);
    }
}
