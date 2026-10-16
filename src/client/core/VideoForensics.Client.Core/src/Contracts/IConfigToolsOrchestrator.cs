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

        Task<(bool Success, string Message)> SetMdnsAdvertisementAsync(IForensicsConfiguration config, bool enabled, CancellationToken ct = default);

        Task<(bool Success, string Message)> FactoryResetAsync(CancellationToken ct = default, string? downloadDirOverride = null, string? dbPathOverride = null);
    }
}
