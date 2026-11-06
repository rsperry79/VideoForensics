using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Client.Core.Contracts
{
    /// <summary>
    /// Configuration changes made from the Settings and Infrastructure pages. Behind an interface so a host can check the
    /// operator before a change is applied; the WebApp does this on the server, since these run in-process.
    /// </summary>
    public interface IConfigToolsOrchestrator
    {
        Task<(bool Success, string Message)> SetRetentionDaysAsync(IForensicsConfiguration config, int days, CancellationToken ct = default);

        Task<(bool Success, string Message)> SetMaxConcurrentDownloadsAsync(IForensicsConfiguration config, int count, CancellationToken ct = default);

        Task<(bool Success, string Message)> SetDownloadLocationAsync(IForensicsConfiguration config, string path, CancellationToken ct = default);

        Task<(bool Success, string Message)> SetReportEnabledAsync(IForensicsConfiguration config, string reportType, bool enabled, CancellationToken ct = default);

        Task<(bool Success, string Message)> SetRedactionLevelAsync(IForensicsConfiguration config, RedactionLevel level, CancellationToken ct = default);

        Task<(bool Success, string Message)> SetKeyStorageProviderAsync(IForensicsConfiguration config, KeyStorageProvider provider, CancellationToken ct = default);

        Task<(bool Success, string Message)> SetLoggingLevelAsync(IForensicsConfiguration config, string level, CancellationToken ct = default);

        Task<(bool Success, string Message)> SetReportFormatAsync(IForensicsConfiguration config, string format, CancellationToken ct = default);

        Task<(bool Success, string Message)> SetHealthSyncAsync(IForensicsConfiguration config, bool enabled, CancellationToken ct = default);

        Task<(bool Success, string Message)> SetScheduledReportsEnabledAsync(IForensicsConfiguration config, bool enabled, CancellationToken ct = default);

        Task<(bool Success, string Message)> SetScheduledReportIntervalHoursAsync(IForensicsConfiguration config, int hours, CancellationToken ct = default);

        Task<(bool Success, string Message)> SetServiceIntervalHoursAsync(IForensicsConfiguration config, string service, int hours, CancellationToken ct = default);

        /// <summary>
        /// Sets how often a time lapse is built from the window's snapshots. Valid range is 1 to 1440 minutes.
        /// </summary>
        /// <param name="config">The configuration to update and save.</param>
        /// <param name="minutes">The rollup interval in minutes.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Success flag and a message. Out-of-range values return failure and are not saved.</returns>
        Task<(bool Success, string Message)> SetTimelapseRollupMinutesAsync(IForensicsConfiguration config, int minutes, CancellationToken ct = default);

        /// <summary>
        /// Sets how long each finished time lapse plays. Valid range is 1 to 600 seconds.
        /// </summary>
        /// <param name="config">The configuration to update and save.</param>
        /// <param name="seconds">The playback length in seconds.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Success flag and a message. Out-of-range values return failure and are not saved.</returns>
        Task<(bool Success, string Message)> SetTimelapsePlaybackSecondsAsync(IForensicsConfiguration config, int seconds, CancellationToken ct = default);

        Task<(bool Success, string Message)> SetMdnsAdvertisementAsync(IForensicsConfiguration config, bool enabled, CancellationToken ct = default);

        Task<(bool Success, string Message)> FactoryResetAsync(CancellationToken ct = default, string? downloadDirOverride = null, string? dbPathOverride = null);
    }
}
