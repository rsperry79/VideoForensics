namespace VideoForensics.Core.Providers.Common.Contracts
{
    /// <summary>One device to pull media for.</summary>
    /// <param name="DeviceId">The provider's device id.</param>
    /// <param name="OutputPath">Directory the media is written to (the account's own media folder).</param>
    /// <param name="ProviderLocationId">The device's provider location id, or null when unknown.</param>
    public sealed record MediaPullDevice(string DeviceId, string OutputPath, string? ProviderLocationId);

    /// <summary>A pull over a time window. The window is split into batches; the watermark advances only past clean batches.</summary>
    /// <param name="WindowStartUtc">Where the pull starts (the watermark for a normal sync, one year ago for the initial backfill).</param>
    /// <param name="WindowEndUtc">Where the pull ends (now, or the end of an explicit range).</param>
    /// <param name="AdvanceWatermark">False for an explicit range, so that run never moves the watermark.</param>
    /// <param name="Devices">The devices to pull media for.</param>
    public sealed record MediaPullRequest(DateTime WindowStartUtc, DateTime WindowEndUtc, bool AdvanceWatermark, IReadOnlyList<MediaPullDevice> Devices);

    /// <summary>What one device gave in a pull. Counts are summed over every batch.</summary>
    /// <param name="DeviceId">The device.</param>
    /// <param name="Pulled">Media downloaded in this run (not already on disk).</param>
    /// <param name="AlreadyPresent">Media that was already on disk and was not fetched again.</param>
    /// <param name="NotPulled">Matched events whose media was not pulled (no recording, failed, or still throttled).</param>
    public sealed record MediaPullDeviceResult(string DeviceId, int Pulled, int AlreadyPresent, int NotPulled);

    /// <summary>Totals for a pull. Only media that was actually fetched or already present counts as pulled.</summary>
    /// <param name="Pulled">Media downloaded in this run.</param>
    /// <param name="AlreadyPresent">Media already on disk (pulled by an earlier run).</param>
    /// <param name="NotPulled">Matched events whose media was not pulled.</param>
    /// <param name="FeedsFailed">Device pulls that failed outright.</param>
    /// <param name="Devices">Per-device results.</param>
    /// <param name="WatermarkUtc">The watermark after the pull (the end of the last clean batch), or null when none was set.</param>
    /// <param name="BatchesCompleted">Batches run to the end without an interruption.</param>
    public sealed record MediaPullResult(
        int Pulled,
        int AlreadyPresent,
        int NotPulled,
        int FeedsFailed,
        IReadOnlyList<MediaPullDeviceResult> Devices,
        DateTime? WatermarkUtc,
        int BatchesCompleted);
}
