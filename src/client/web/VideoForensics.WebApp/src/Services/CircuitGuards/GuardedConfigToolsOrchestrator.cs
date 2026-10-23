using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Core.Contracts;
using VideoForensics.Ui.Shared.Layout;
using VideoForensics.WebApp.Contracts;

namespace VideoForensics.WebApp.Services.CircuitGuards
{
    /// <summary>
    /// Guards server configuration changes made from the Settings and Infrastructure pages. These run in-process, and no
    /// HTTP endpoint covers them, so the server check is the only one. Download location, key storage and factory reset
    /// need SuperAdminLocal; the other settings need Admin. A denied change returns a failed result, not an exception,
    /// so the page can show the reason.
    /// </summary>
    public sealed class GuardedConfigToolsOrchestrator(IConfigToolsOrchestrator inner, ICircuitOperatorGate gate) : IConfigToolsOrchestrator
    {
        public Task<(bool Success, string Message)> SetRetentionDaysAsync(IForensicsConfiguration config, int days, CancellationToken ct = default) =>
            Run(AccessPolicy.Admin, () => inner.SetRetentionDaysAsync(config, days, ct));

        public Task<(bool Success, string Message)> SetMaxConcurrentDownloadsAsync(IForensicsConfiguration config, int count, CancellationToken ct = default) =>
            Run(AccessPolicy.Admin, () => inner.SetMaxConcurrentDownloadsAsync(config, count, ct));

        public Task<(bool Success, string Message)> SetDownloadLocationAsync(IForensicsConfiguration config, string path, CancellationToken ct = default) =>
            Run(AccessPolicy.SuperAdminLocal, () => inner.SetDownloadLocationAsync(config, path, ct));

        public Task<(bool Success, string Message)> SetReportEnabledAsync(IForensicsConfiguration config, string reportType, bool enabled, CancellationToken ct = default) =>
            Run(AccessPolicy.Admin, () => inner.SetReportEnabledAsync(config, reportType, enabled, ct));

        public Task<(bool Success, string Message)> SetRedactionLevelAsync(IForensicsConfiguration config, RedactionLevel level, CancellationToken ct = default) =>
            Run(AccessPolicy.Admin, () => inner.SetRedactionLevelAsync(config, level, ct));

        public Task<(bool Success, string Message)> SetKeyStorageProviderAsync(IForensicsConfiguration config, KeyStorageProvider provider, CancellationToken ct = default) =>
            Run(AccessPolicy.SuperAdminLocal, () => inner.SetKeyStorageProviderAsync(config, provider, ct));

        public Task<(bool Success, string Message)> SetLoggingLevelAsync(IForensicsConfiguration config, string level, CancellationToken ct = default) =>
            Run(AccessPolicy.Admin, () => inner.SetLoggingLevelAsync(config, level, ct));

        public Task<(bool Success, string Message)> SetReportFormatAsync(IForensicsConfiguration config, string format, CancellationToken ct = default) =>
            Run(AccessPolicy.Admin, () => inner.SetReportFormatAsync(config, format, ct));

        public Task<(bool Success, string Message)> SetHealthSyncAsync(IForensicsConfiguration config, bool enabled, CancellationToken ct = default) =>
            Run(AccessPolicy.Admin, () => inner.SetHealthSyncAsync(config, enabled, ct));

        public Task<(bool Success, string Message)> SetMdnsAdvertisementAsync(IForensicsConfiguration config, bool enabled, CancellationToken ct = default) =>
            Run(AccessPolicy.Admin, () => inner.SetMdnsAdvertisementAsync(config, enabled, ct));

        public Task<(bool Success, string Message)> FactoryResetAsync(CancellationToken ct = default, string? downloadDirOverride = null, string? dbPathOverride = null) =>
            Run(AccessPolicy.SuperAdminLocal, () => inner.FactoryResetAsync(ct, downloadDirOverride, dbPathOverride));

        private async Task<(bool Success, string Message)> Run(AccessPolicy policy, Func<Task<(bool Success, string Message)>> change)
        {
            if (!gate.Allows(policy))
            {
                return (false, gate.Denial(policy));
            }

            return await change();
        }
    }
}
