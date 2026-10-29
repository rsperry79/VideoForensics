using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Client.Ui.Shared.Layout;

/// <summary>
/// Decides whether the signed-in operator may change a restricted settings page, from its stored level. It mirrors
/// SettingsAccessPolicy on the server. The default is SuperAdmin: only an explicit "Admin" opens a page to admins.
/// The server stays the authority; this only keeps the page from offering actions that fail.
/// </summary>
public static class SettingsAccessGate
{
    /// <summary>True when <paramref name="ctx"/> meets the level in <paramref name="storedLevel"/>. A missing or unknown value means SuperAdmin.</summary>
    public static bool CanChange(string? storedLevel, NavContext ctx)
    {
        AccessPolicy required = storedLevel == ProviderEditAccessLevels.Admin ? AccessPolicy.Admin : AccessPolicy.SuperAdmin;
        return PageAccessRules.Allows(required, ctx);
    }
}
