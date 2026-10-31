using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Ui.Shared.Layout;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Layout;

public class PageAccessRules_Tests
{
    private static NavContext Ctx(OperatorRole? role, bool local = false, bool passkey = false, bool signedIn = true)
    {
        return new(signedIn, role, AppLockSupported: false, IsLocalTier: local, IsPasskeySession: passkey);
    }

    [Fact]
    public void IsAllowed_LocalOnlyPage_RemoteSuperAdmin_Denied()
    {
        Assert.False(PageAccessRules.IsAllowed("/settings/network", Ctx(OperatorRole.SuperAdmin)));
    }

    [Fact]
    public void IsAllowed_LocalOnlyPage_LocalSuperAdmin_Allowed()
    {
        Assert.True(PageAccessRules.IsAllowed("/settings/network", Ctx(OperatorRole.SuperAdmin, local: true)));
    }

    [Fact]
    public void IsAllowed_LocalOnlyPage_RemotePasskeySuperAdmin_Denied()
    {
        // A passkey satisfies SuperAdminLocalOrPasskey only; pages on SuperAdminLocal stay Local-tier-only.
        Assert.False(PageAccessRules.IsAllowed("/settings/network", Ctx(OperatorRole.SuperAdmin, passkey: true)));
    }

    [Fact]
    public void IsAllowed_LocalOnlyPage_LocalAdmin_Denied()
    {
        Assert.False(PageAccessRules.IsAllowed("/settings/network", Ctx(OperatorRole.Admin, local: true)));
    }

    [Fact]
    public void Allows_SuperAdminLocalOrPasskey_RemotePasskeySuperAdmin_Allowed()
    {
        Assert.True(PageAccessRules.Allows(AccessPolicy.SuperAdminLocalOrPasskey, Ctx(OperatorRole.SuperAdmin, passkey: true)));
    }

    [Fact]
    public void Allows_SuperAdminLocalOrPasskey_RemotePasswordAdmin_Denied()
    {
        Assert.False(PageAccessRules.Allows(AccessPolicy.SuperAdminLocalOrPasskey, Ctx(OperatorRole.SuperAdmin)));
    }

    [Fact]
    public void IsAllowed_AdminPage_ReadOnlyOperator_Denied()
    {
        Assert.False(PageAccessRules.IsAllowed("/settings/notifications", Ctx(OperatorRole.ReadOnly)));
    }

    [Fact]
    public void IsAllowed_NotificationSettingsPage_AdminOperator_DeniedUntilOpened()
    {
        Assert.False(PageAccessRules.IsAllowed("/settings/notifications", Ctx(OperatorRole.Admin)));
        Assert.True(PageAccessRules.IsAllowed("/settings/notifications", Ctx(OperatorRole.Admin) with { OpenToAdmin = RestrictedSettings.Notifications }));
        Assert.True(PageAccessRules.IsAllowed("/settings/notifications", Ctx(OperatorRole.SuperAdmin)));
    }

    [Fact]
    public void IsAllowed_SettingsPages_AdminOperator_FollowTheirOwnOpenFlag()
    {
        Assert.False(PageAccessRules.IsAllowed("/settings/reports", Ctx(OperatorRole.Admin) with { OpenToAdmin = RestrictedSettings.Email }));
        Assert.True(PageAccessRules.IsAllowed("/settings/reports", Ctx(OperatorRole.Admin) with { OpenToAdmin = RestrictedSettings.Reports }));
        Assert.True(PageAccessRules.IsAllowed("/settings/integrations/email", Ctx(OperatorRole.Admin) with { OpenToAdmin = RestrictedSettings.Email }));
        Assert.True(PageAccessRules.IsAllowed("/settings/llm", Ctx(OperatorRole.Admin) with { OpenToAdmin = RestrictedSettings.Llm }));
        Assert.False(PageAccessRules.IsAllowed("/settings/llm", Ctx(OperatorRole.Admin)));
        Assert.True(PageAccessRules.IsAllowed("/settings/llm", Ctx(OperatorRole.SuperAdmin)));
    }

    [Fact]
    public void IsAllowed_AnyPolicyPage_SignedOut_Denied()
    {
        Assert.False(PageAccessRules.IsAllowed("/chat", Ctx(role: null, signedIn: false)));
    }

    [Fact]
    public void IsAllowed_UnlistedPath_AllowedWithoutRole()
    {
        // Pages without a rule keep their existing behaviour; only listed pages are gated.
        Assert.True(PageAccessRules.IsAllowed("/settings", Ctx(role: null, signedIn: false)));
    }

    [Fact]
    public void IsAllowed_PathWithQuery_UsesPathRule()
    {
        Assert.False(PageAccessRules.IsAllowed("/settings/logs?level=error", Ctx(OperatorRole.SuperAdmin)));
        Assert.True(PageAccessRules.IsAllowed("/settings/logs?level=error", Ctx(OperatorRole.SuperAdmin, local: true)));
    }

    [Fact]
    public void TryGetPolicy_ListedPath_ReturnsPolicy()
    {
        Assert.True(PageAccessRules.TryGetPolicy("/settings/operators", out AccessPolicy policy));
        Assert.Equal(AccessPolicy.SuperAdminLocal, policy);
    }

    [Fact]
    public void TryGetPolicy_UnlistedPath_ReturnsFalse()
    {
        Assert.False(PageAccessRules.TryGetPolicy("/settings", out _));
    }
}

public class NavGroupsSections_Tests
{
    private static NavContext Ctx(OperatorRole? role, bool local = false)
    {
        return new(IsSignedIn: role is not null, Role: role, AppLockSupported: false, IsLocalTier: local);
    }

    private static NavGroup Admin => NavGroups.All.First(g => g.Key == "admin");

    private static IReadOnlyList<string> VisibleAdminPaths(NavContext ctx)
    {
        return Admin.Items.Where(i => i.IsVisible(ctx)).Select(i => i.Path).ToList();
    }

    [Fact]
    public void AdminTab_ReadOnlyOperator_IsVisible()
    {
        Assert.True(Admin.IsVisible(Ctx(OperatorRole.ReadOnly)));
    }

    [Theory]
    [InlineData("/settings/network")]
    [InlineData("/settings/devices")]
    [InlineData("/settings/storage")]
    [InlineData("/settings/logs")]
    [InlineData("/settings/lockout-policy")]
    [InlineData("/settings/auth-methods")]
    [InlineData("/settings/update-check")]
    public void AdminItems_RemoteSuperAdmin_HidesLocalOnlyPages(string path)
    {
        Assert.DoesNotContain(path, VisibleAdminPaths(Ctx(OperatorRole.SuperAdmin)));
    }

    [Theory]
    [InlineData("/settings/network")]
    [InlineData("/settings/logs")]
    [InlineData("/settings/lockout-policy")]
    public void AdminItems_LocalSuperAdmin_ShowsLocalOnlyPages(string path)
    {
        Assert.Contains(path, VisibleAdminPaths(Ctx(OperatorRole.SuperAdmin, local: true)));
    }

    [Theory]
    [InlineData("/settings/infrastructure")]
    [InlineData("/tools/import-export")]
    [InlineData("/settings/notifications")]
    [InlineData("/settings/security-log")]
    [InlineData("/settings/operators")]
    [InlineData("/settings/llm")]
    public void AdminItems_ReadOnlyOperator_HidesAdminOnlyPages(string path)
    {
        Assert.DoesNotContain(path, VisibleAdminPaths(Ctx(OperatorRole.ReadOnly)));
    }

    [Fact]
    public void AdminItems_ReadOnlyOperator_ShowsSecurityEventsAndGeneral()
    {
        IReadOnlyList<string> paths = VisibleAdminPaths(Ctx(OperatorRole.ReadOnly));
        Assert.Contains("/settings", paths);
        Assert.Contains("/settings/security-events", paths);
    }

    [Fact]
    public void SectionsFor_AdminGroup_LocalSuperAdmin_ReturnsTitledSectionsInOrder()
    {
        IReadOnlyList<NavSection> sections = NavGroups.SectionsFor(Admin, Ctx(OperatorRole.SuperAdmin, local: true));

        Assert.Equal(
            new[] { "General", "Sources", "Security", "Diagnostics & Updates", "Devices & Network", "Storage & Data", "Integrations" },
            sections.Select(s => s.Title).ToList());
    }

    [Fact]
    public void SectionsFor_AdminGroup_ReadOnlyOperator_DropsEmptySections()
    {
        var titles = NavGroups.SectionsFor(Admin, Ctx(OperatorRole.ReadOnly)).Select(s => s.Title).ToList();

        Assert.DoesNotContain("Integrations", titles);
        Assert.DoesNotContain("Devices & Network", titles);
        Assert.Contains("General", titles);
        Assert.Contains("Security", titles);
    }

    [Fact]
    public void SectionsFor_AdminGroup_RemoteSuperAdmin_DropsLocalOnlyItemsFromSections()
    {
        IReadOnlyList<NavSection> sections = NavGroups.SectionsFor(Admin, Ctx(OperatorRole.SuperAdmin));
        NavSection? diagnostics = sections.FirstOrDefault(s => s.Title == "Diagnostics & Updates");

        // Server Logs and App Update are Local-only, so the section has nothing to show off-Local.
        Assert.Null(diagnostics);
    }

    [Fact]
    public void SectionsFor_GroupWithoutSections_ReturnsSingleUntitledSection()
    {
        var group = new NavGroup("plain", "Plain", "/plain", [new("Only item", "/plain")]);

        IReadOnlyList<NavSection> sections = NavGroups.SectionsFor(group, Ctx(OperatorRole.ReadOnly));

        _ = Assert.Single(sections);
        Assert.Null(sections[0].Title);
    }
}
