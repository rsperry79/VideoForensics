using VideoForensics.Client.Ui.Shared.Layout;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Client.WebApp.Services
{
    /// <summary>Resolves the operator level required to use one restricted settings page, from the stored permissions setting.</summary>
    public interface ISettingsAccessPolicy
    {
        /// <summary>Returns <see cref="AccessPolicy.SuperAdmin"/> or <see cref="AccessPolicy.Admin"/> for the current setting of <paramref name="area"/>.</summary>
        Task<AccessPolicy> GetRequiredPolicyAsync(RestrictedSettings area, CancellationToken ct);
    }

    /// <summary>
    /// Reads each restricted page's level from the app-setting store on every call, so a change takes effect on the next
    /// request. All of them default to SuperAdmin: only an explicit "Admin" opens a page to admins, and a missing or
    /// unrecognised value never widens access.
    /// </summary>
    public sealed class SettingsAccessPolicy(IAppSettingRepository settings) : ISettingsAccessPolicy
    {
        /// <summary>App-setting key holding the stored level for <paramref name="area"/>. These names are already stored, so they do not change.</summary>
        public static string SettingKeyFor(RestrictedSettings area)
        {
            return area switch
            {
                RestrictedSettings.Reports => "Permissions.ReportSettings",
                RestrictedSettings.Email => "Permissions.EmailSettings",
                RestrictedSettings.Notifications => "Permissions.NotificationSettings",
                RestrictedSettings.Llm => "Permissions.LlmSettings",
                RestrictedSettings.Infrastructure => "Permissions.InfrastructureSettings",
                _ => throw new ArgumentOutOfRangeException(nameof(area), area, "Not a single restricted settings page."),
            };
        }

        public async Task<AccessPolicy> GetRequiredPolicyAsync(RestrictedSettings area, CancellationToken ct)
        {
            return ToPolicy(await settings.GetAsync(SettingKeyFor(area), ct));
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
