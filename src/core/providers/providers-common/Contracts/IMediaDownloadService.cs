namespace VideoForensics.Providers.Common.Contracts
{
    /// <summary>Platform-agnostic media download interface</summary>
    public interface IMediaDownloadService
    {
        /// <summary>Downloads videos for a device within a date range</summary>
        /// <param name="providerLocationId">
        /// The device's real provider-reported location id, so the provider can attribute the
        /// resulting DB records to that location (and the caller's already-resolved account) instead
        /// of falling back to a synthetic placeholder. Null only when the caller genuinely doesn't
        /// know the device's location.
        /// </param>
        Task<DownloadResult> DownloadVideosAsync(
            string deviceId,
            string outputPath,
            DateTime startDate,
            DateTime endDate,
            string? providerLocationId = null,
            CancellationToken cancellationToken = default
        );

        /// <summary>Downloads snapshots for a device within a date range</summary>
        /// <param name="providerLocationId">See DownloadVideosAsync's parameter of the same name.</param>
        Task<DownloadResult> DownloadSnapshotsAsync(
            string deviceId,
            string outputPath,
            DateTime startDate,
            DateTime endDate,
            string? providerLocationId = null,
            CancellationToken cancellationToken = default
        );

        /// <summary>
        /// Tells the provider which account any device/media DB records it creates from here on
        /// should be attributed to. Without this, a provider with no other way to know the currently
        /// active account would otherwise have to fall back to a synthetic placeholder account - a
        /// no-op for a provider that doesn't need this (e.g. it's given the account context another
        /// way, or doesn't persist to a shared DB at all).
        /// </summary>
        void SetActiveProviderAccountId(Guid accountId) { }

        /// <summary>Gets current download status</summary>
        DownloadStatus GetStatus();

        /// <summary>
        /// Counts how many items match the query (e.g. events in range for this device) without
        /// downloading anything. Lets a caller learn the true total across multiple devices up front
        /// — e.g. to size an aggregate progress bar before the first device's download even starts —
        /// instead of only finding out each device's count as its turn in a sequential loop arrives.
        /// </summary>
        Task<int> GetMatchedEventCountAsync(
            string deviceId,
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(0);

        /// <summary>
        /// True when a GetMatchedEventCountAsync/DownloadVideosAsync call for this exact range
        /// would be served entirely from already-fetched history data, with no API request. Lets a
        /// caller iterating multiple devices skip a rate-limit-safety delay it only needs before a
        /// call that will actually hit the network. Defaults to false (assume not cached) so a
        /// provider that hasn't implemented this keeps the conservative delay.
        /// </summary>
        bool IsHistoryCached(DateTime startDate, DateTime endDate) => false;

        /// <summary>
        /// If the provider's API has hard-banned this account (repeated rate-limit violations), the
        /// UTC time that ban expires; null if not currently banned. Lets a caller check up front and
        /// fail fast with a clear message instead of only finding out after every device's own retry
        /// loop runs to exhaustion first. Defaults to null (no ban tracking) for a provider that
        /// hasn't implemented this.
        /// </summary>
        DateTime? GetRateLimitBanUntilUtc() => null;

        /// <summary>
        /// Explicitly lifts an active rate-limit ban for one more attempt, at the caller's request
        /// (e.g. the user was prompted and chose to try anyway despite the ban). A no-op for a
        /// provider without ban tracking.
        /// </summary>
        void OverrideRateLimitBan() { }

        /// <summary>
        /// Sets how many files may download concurrently within a single device's batch (devices
        /// themselves are still processed sequentially by the caller). Values below 1 are ignored.
        /// </summary>
        void SetMaxConcurrentDownloads(int value) { }

        /// <summary>
        /// Returns and clears any per-item activity messages (e.g. "downloaded X", "failed: Y")
        /// queued since the last call, so a caller can poll this alongside GetStatus() to show a
        /// live feed of individual file outcomes during a download in progress.
        /// </summary>
        IReadOnlyList<string> DrainActivityLog() => Array.Empty<string>();
    }

    /// <summary>Result of a download operation</summary>
    public record DownloadResult(
        bool Success,
        int FilesDownloaded = 0,
        long BytesDownloaded = 0,
        string? ErrorMessage = null,
        int MetadataFilesWritten = 0,
        int MediaFilesValidated = 0,
        int MediaErrorsDetected = 0,
        int MediaErrorsCorrected = 0,
        List<string>? ValidatedFiles = null,
        List<string>? CorrectedFiles = null,
        /// <summary>
        /// How many items matched the query (e.g. events in range for this device), independent of
        /// how many actually got downloaded. Lets a caller detect "some were skipped" (rate limit,
        /// cancellation) via FilesMatched - FilesDownloaded, even when Success is true.
        /// </summary>
        int FilesMatched = 0,
        /// <summary>
        /// Why FilesDownloaded is less than FilesMatched (e.g. "rate limited", "3 item(s) failed"),
        /// populated whenever that gap is nonzero even though Success is true. Null when the gap is
        /// zero, or when the provider hasn't been updated to classify it - callers should not assume
        /// a null reason means nothing was skipped, only that the cause is unknown.
        /// </summary>
        string? SkipReason = null
    );

    /// <summary>Metadata about a downloaded media file</summary>
    public record MediaMetadata(
        string FileName,
        string DeviceId,
        string DeviceName,
        DateTime RecordedAt,
        long FileSizeBytes,
        string MediaFormat,
        string? LocationId = null,
        string? LocationName = null,
        string? EventType = null,
        Dictionary<string, string>? CustomProperties = null
    );

    /// <summary>Current download status</summary>
    public record DownloadStatus(
        bool IsDownloading,
        int FilesCompleted,
        int FilesTotal,
        long BytesDownloaded,
        string? CurrentFile = null,
        int TotalFilesCompleted = 0,
        int TotalFilesMatched = 0,
        long TotalBytesDownloaded = 0,
        int ActiveConnections = 1,
        double CurrentSpeedMbps = 0
    );
}
