using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for live-view telemetry samples (RTP/RTCP metrics).</summary>
    public interface ILiveViewTelemetryRepository
    {
        /// <summary>Appends a new telemetry sample to the database.</summary>
        Task AddSampleAsync(LiveViewTelemetrySample sample, CancellationToken ct);

        /// <summary>Gets all telemetry samples for a live-view session.</summary>
        Task<IReadOnlyList<LiveViewTelemetrySample>> GetSamplesAsync(Guid sessionId, CancellationToken ct);

        /// <summary>Deletes telemetry samples older than the specified age.</summary>
        Task<int> PruneOlderThanAsync(TimeSpan age, CancellationToken ct);
    }
}
