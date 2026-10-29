using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Core.Contracts;
using VideoForensics.Client.WebApp.Contracts;
using VideoForensics.Client.Ui.Shared.Layout;
using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Client.WebApp.Services.CircuitGuards
{
    /// <summary>
    /// Guards server configuration changes made from the Settings and Infrastructure pages. These run in-process, and no
    /// HTTP endpoint covers them, so the server check is the only one. Download location, key storage and factory reset
    /// need SuperAdminLocal; report settings need the level set on the Permissions page (SuperAdmin unless opened to Admin); the other settings need Admin. A denied change returns a failed result, not an exception,
    /// so the page can show the reason.
    /// </summary>
    public sealed class GuardedConfigToolsOrchestrator(IConfigToolsOrchestrator inner, ICircuitOperatorGate gate, ServerLogLevel? serverLogLevel = null, ISettingsAccessPolicy? accessPolicy = null) : IConfigToolsOrchestrator
    {
        public async Task<(bool Success, string Message)> SetLoggingLevelAsync(IForensicsConfiguration config, string level, CancellationToken ct = default)
        {
            (bool Success, string Message) result = await Run(AccessPolicy.Admin, () => inner.SetLoggingLevelAsync(config, level, ct));
            if (result.Success)
            {
                // The saved level is applied now, so the console and audit files follow it without a restart.
                serverLogLevel?.Apply(level);
            }

            return result;
        }

        public Task<(bool Success, string Message)> SetRetentionDaysAsync(IForensicsConfiguration config, int days, CancellationToken ct = default)
        {
            return Run(AccessPolicy.Admin, () => inner.SetRetentionDaysAsync(config, days, ct));
        }

        public Task<(bool Success, string Message)> SetMaxConcurrentDownloadsAsync(IForensicsConfiguration config, int count, CancellationToken ct = default)
        {
            return Run(AccessPolicy.Admin, () => inner.SetMaxConcurrentDownloadsAsync(config, count, ct));
        }

        public Task<(bool Success, string Message)> SetDownloadLocationAsync(IForensicsConfiguration config, string path, CancellationToken ct = default)
        {
            return Run(AccessPolicy.SuperAdminLocal, () => inner.SetDownloadLocationAsync(config, path, ct));
        }

        public Task<(bool Success, string Message)> SetReportEnabledAsync(IForensicsConfiguration config, string reportType, bool enabled, CancellationToken ct = default)
        {
            return RunReportSetting(() => inner.SetReportEnabledAsync(config, reportType, enabled, ct), ct);
        }

        public Task<(bool Success, string Message)> SetRedactionLevelAsync(IForensicsConfiguration config, RedactionLevel level, CancellationToken ct = default)
        {
            return RunReportSetting(() => inner.SetRedactionLevelAsync(config, level, ct), ct);
        }

        public Task<(bool Success, string Message)> SetKeyStorageProviderAsync(IForensicsConfiguration config, KeyStorageProvider provider, CancellationToken ct = default)
        {
            return Run(AccessPolicy.SuperAdminLocal, () => inner.SetKeyStorageProviderAsync(config, provider, ct));
        }

        public Task<(bool Success, string Message)> SetReportFormatAsync(IForensicsConfiguration config, string format, CancellationToken ct = default)
        {
            return RunReportSetting(() => inner.SetReportFormatAsync(config, format, ct), ct);
        }

        public Task<(bool Success, string Message)> SetHealthSyncAsync(IForensicsConfiguration config, bool enabled, CancellationToken ct = default)
        {
            return Run(AccessPolicy.Admin, () => inner.SetHealthSyncAsync(config, enabled, ct));
        }

        public Task<(bool Success, string Message)> SetMdnsAdvertisementAsync(IForensicsConfiguration config, bool enabled, CancellationToken ct = default)
        {
            return Run(AccessPolicy.Admin, () => inner.SetMdnsAdvertisementAsync(config, enabled, ct));
        }

        public Task<(bool Success, string Message)> FactoryResetAsync(CancellationToken ct = default, string? downloadDirOverride = null, string? dbPathOverride = null)
        {
            return Run(AccessPolicy.SuperAdminLocal, () => inner.FactoryResetAsync(ct, downloadDirOverride, dbPathOverride));
        }

        private async Task<(bool Success, string Message)> RunReportSetting(Func<Task<(bool Success, string Message)>> change, CancellationToken ct)
        {
            // Without a policy store the safe default applies: SuperAdmin only.
            AccessPolicy required = accessPolicy is null ? AccessPolicy.SuperAdmin : await accessPolicy.GetRequiredPolicyAsync(RestrictedSettings.Reports, ct);
            return await Run(required, change);
        }

        private async Task<(bool Success, string Message)> Run(AccessPolicy policy, Func<Task<(bool Success, string Message)>> change)
        {
            return !gate.Allows(policy) ? (false, gate.Denial(policy)) : await change();
        }
    }
}
