using VideoForensics.Client.Ui.Shared.Layout;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.WebApp.Services
{
    /// <summary>Resolves the operator level required to edit provider accounts, from the stored permissions setting.</summary>
    public interface IProviderAccountEditPolicy
    {
        /// <summary>Returns <see cref="AccessPolicy.Admin"/> or <see cref="AccessPolicy.SuperAdmin"/> for the current setting.</summary>
        Task<AccessPolicy> GetRequiredPolicyAsync(CancellationToken ct);
    }

    /// <summary>
    /// Reads the provider-account edit setting from the app-setting store on every call. It is not cached, so a change
    /// on the Permissions page takes effect on the next request. Missing or unrecognised values fall back to Admin: the
    /// setting may narrow edits to SuperAdmin, but a bad value must not lock out every admin or widen access below Admin.
    /// </summary>
    public sealed class ProviderAccountEditPolicy(IAppSettingRepository settings) : IProviderAccountEditPolicy
    {
        /// <summary>App-setting key holding the stored edit level.</summary>
        public const string SettingKey = "Permissions.ProviderAccountEdit";

        public async Task<AccessPolicy> GetRequiredPolicyAsync(CancellationToken ct)
        {
            return ToPolicy(await settings.GetAsync(SettingKey, ct));
        }

        /// <summary>Maps a stored or submitted level to the level the value asks for. Anything but "SuperAdmin" is Admin.</summary>
        public static string NormalizeLevel(string? stored)
        {
            return stored == ProviderEditAccessLevels.SuperAdmin ? ProviderEditAccessLevels.SuperAdmin : ProviderEditAccessLevels.Admin;
        }

        public static AccessPolicy ToPolicy(string? stored)
        {
            return NormalizeLevel(stored) == ProviderEditAccessLevels.SuperAdmin ? AccessPolicy.SuperAdmin : AccessPolicy.Admin;
        }
    }

    /// <summary>Role comparison for provider-account edits, kept separate from HTTP so it can be tested directly.</summary>
    public static class ProviderAccountEditRules
    {
        /// <summary>True when <paramref name="role"/> meets <paramref name="required"/>. Roles are ordered, so this is a >= comparison.</summary>
        public static bool IsAllowed(AccessPolicy required, OperatorRole role)
        {
            OperatorRole minimum = required == AccessPolicy.SuperAdmin ? OperatorRole.SuperAdmin : OperatorRole.Admin;
            return role >= minimum;
        }
    }
}
