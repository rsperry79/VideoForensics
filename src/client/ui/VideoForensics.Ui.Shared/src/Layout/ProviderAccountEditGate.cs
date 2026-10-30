using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Client.Ui.Shared.Layout;

/// <summary>
/// Decides whether the signed-in operator may change provider accounts, from the stored edit level. It mirrors
/// ProviderAccountEditPolicy on the server, so the UI hides controls the server would refuse. The server stays the
/// authority; this only keeps the page from offering actions that would fail.
/// </summary>
public static class ProviderAccountEditGate
{
    /// <summary>
    /// True when <paramref name="ctx"/> meets the level in <paramref name="storedLevel"/>. A missing or unknown value
    /// means Admin, the same default the server applies, so a bad value never hides controls from an admin.
    /// </summary>
    public static bool CanEdit(string? storedLevel, NavContext ctx)
    {
        AccessPolicy required = storedLevel == ProviderEditAccessLevels.SuperAdmin ? AccessPolicy.SuperAdmin : AccessPolicy.Admin;
        return PageAccessRules.Allows(required, ctx);
    }
}
