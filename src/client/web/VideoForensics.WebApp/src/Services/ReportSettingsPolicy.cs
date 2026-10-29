using VideoForensics.Client.Ui.Shared.Layout;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Client.WebApp.Services
{
    /// <summary>Resolves the operator level required to change report settings, from the stored permissions setting.</summary>
    public interface IReportSettingsPolicy
    {
        /// <summary>Returns <see cref="AccessPolicy.SuperAdmin"/> or <see cref="AccessPolicy.Admin"/> for the current setting.</summary>
        Task<AccessPolicy> GetRequiredPolicyAsync(CancellationToken ct);
    }

    /// <summary>
    /// Reads the report-settings level from the app-setting store on every call, so a change takes effect on the next
    /// request. Unlike provider edits this defaults to SuperAdmin: only an explicit "Admin" opens report settings to
    /// admins, and a missing or unrecognised value never widens access.
    /// </summary>
    public sealed class ReportSettingsPolicy(IAppSettingRepository settings) : IReportSettingsPolicy
    {
        /// <summary>App-setting key holding the stored report-settings level.</summary>
        public const string SettingKey = "Permissions.ReportSettings";

        public async Task<AccessPolicy> GetRequiredPolicyAsync(CancellationToken ct)
        {
            return ToPolicy(await settings.GetAsync(SettingKey, ct));
        }

        /// <summary>Maps a stored or submitted level to a known level. Anything but "Admin" is SuperAdmin.</summary>
        public static string NormalizeLevel(string? stored)
        {
            return stored == ProviderEditAccessLevels.Admin ? ProviderEditAccessLevels.Admin : ProviderEditAccessLevels.SuperAdmin;
        }

        public static AccessPolicy ToPolicy(string? stored)
        {
            return NormalizeLevel(stored) == ProviderEditAccessLevels.Admin ? AccessPolicy.Admin : AccessPolicy.SuperAdmin;
        }
    }
}
