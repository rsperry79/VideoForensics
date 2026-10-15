namespace VideoForensics.Ui.Shared.Tests;

using System.Resources;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Layout;
using VideoForensics.Ui.Shared.Resources;
using Xunit;

/// <summary>
/// Pins the reviewed policy for every admin page. Each row was checked against the endpoints the page calls (see the
/// comment on the matching row in <see cref="PageAccessRules"/>). Changing a row must be a deliberate review decision.
/// </summary>
public class AdminNavConfiguration_Tests
{
    private static NavContext Ctx(OperatorRole? role, bool local = false, bool passkey = false) =>
        new(role is not null, role, AppLockSupported: false, IsLocalTier: local, IsPasskeySession: passkey);

    [Theory]
    [InlineData("/settings/security-events", AccessPolicy.ReadOnly)]
    [InlineData("/settings/operators", AccessPolicy.SuperAdminLocal)]
    [InlineData("/settings/operators/{Id:guid}", AccessPolicy.SuperAdminLocal)]
    [InlineData("/admin/pending-credentials", AccessPolicy.SuperAdminLocalOrPasskey)]
    [InlineData("/settings/infrastructure", AccessPolicy.SuperAdminLocal)]
    [InlineData("/tools/import-export", AccessPolicy.SuperAdminLocal)]
    [InlineData("/settings/notifications", AccessPolicy.Admin)]
    [InlineData("/settings/security-log", AccessPolicy.Admin)]
    [InlineData("/settings/devices", AccessPolicy.SuperAdminLocal)]
    [InlineData("/settings/network", AccessPolicy.SuperAdminLocal)]
    [InlineData("/settings/storage", AccessPolicy.SuperAdminLocal)]
    [InlineData("/settings/update-check", AccessPolicy.SuperAdminLocal)]
    [InlineData("/settings/logs", AccessPolicy.SuperAdminLocal)]
    [InlineData("/settings/llm", AccessPolicy.SuperAdmin)]
    [InlineData("/settings/lockout-policy", AccessPolicy.SuperAdminLocal)]
    [InlineData("/settings/auth-methods", AccessPolicy.SuperAdminLocal)]
    [InlineData("/accounts", AccessPolicy.SuperAdminLocal)]
    [InlineData("/devices/config", AccessPolicy.SuperAdminLocal)]
    [InlineData("/review/export", AccessPolicy.SuperAdminLocal)]
    public void PageAccessRules_ListedPage_HasReviewedPolicy(string path, AccessPolicy expected)
    {
        Assert.True(PageAccessRules.TryGetPolicy(path, out AccessPolicy actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("/settings/operators")]
    [InlineData("/tools/import-export")]
    [InlineData("/settings/infrastructure")]
    public void PageAccessRules_RemoteAdmin_CannotOpenLocalOnlyAdminPage(string path)
    {
        Assert.False(PageAccessRules.IsAllowed(path, Ctx(OperatorRole.Admin)));
    }

    [Fact]
    public void PageAccessRules_PendingApprovals_RemotePasskeySuperAdmin_Allowed()
    {
        // Approving credentials is open to a passkey session from any tier (SuperAdminLocalOrPasskey).
        Assert.True(PageAccessRules.IsAllowed("/admin/pending-credentials", Ctx(OperatorRole.SuperAdmin, passkey: true)));
    }

    [Fact]
    public void PageAccessRules_PendingApprovals_RemotePasswordSuperAdmin_Denied()
    {
        Assert.False(PageAccessRules.IsAllowed("/admin/pending-credentials", Ctx(OperatorRole.SuperAdmin)));
    }

    [Fact]
    public void PageAccessRules_OperatorDetailsTemplate_RemoteSuperAdmin_Denied()
    {
        // The guard checks the @page template, so the parameterized template must be listed.
        Assert.False(PageAccessRules.IsAllowed("/settings/operators/{Id:guid}", Ctx(OperatorRole.SuperAdmin)));
        Assert.True(PageAccessRules.IsAllowed("/settings/operators/{Id:guid}", Ctx(OperatorRole.SuperAdmin, local: true)));
    }

    [Fact]
    public void NavGroups_AdminItems_EveryPathIsGatedOrExplicitlyOpen()
    {
        // Only these admin paths are deliberately unrestricted. Any other admin nav path must have a table row,
        // so a new admin page cannot ship ungated by accident.
        string[] explicitlyOpen = ["/settings", "/settings/app-lock"];
        NavGroup admin = NavGroups.All.First(g => g.Key == "admin");

        foreach (NavItem item in admin.Items)
        {
            bool gated = PageAccessRules.TryGetPolicy(item.Path, out _);
            Assert.True(gated || explicitlyOpen.Contains(item.Path), $"Admin nav path {item.Path} has no access rule.");
        }
    }

    [Fact]
    public void SectionsFor_NonAdjacentItemsWithSameHeading_MergeIntoOneSection()
    {
        IReadOnlyList<NavItem> items =
        [
            new("Operators", "/settings/operators", Section: "Security"),
            new("General", "/settings"),
            new("Login Methods", "/settings/auth-methods", Section: "Security"),
        ];
        var group = new NavGroup("test", "Test", "/settings", items);

        var sections = NavGroups.SectionsFor(group, new NavContext(true, OperatorRole.SuperAdmin, false, IsLocalTier: true));

        Assert.Equal(2, sections.Count);
        Assert.Equal("Security", sections[0].Title);
        Assert.Equal(2, sections[0].Items.Count);
        Assert.Null(sections[1].Title);
    }

    [Fact]
    public void Localization_AdminNavLabelsAndSectionTitles_ExistInSharedResources()
    {
        var resources = new ResourceManager(typeof(SharedResources));
        NavGroup admin = NavGroups.All.First(g => g.Key == "admin");

        var keys = admin.Items.Select(i => i.Text)
            .Concat(admin.Items.Where(i => i.Section is not null).Select(i => i.Section!))
            .Concat(["Admin", "Settings", "Navigation"])
            .Distinct();

        foreach (string key in keys)
        {
            Assert.False(string.IsNullOrEmpty(resources.GetString(key)), $"Missing SharedResources entry for \"{key}\".");
        }
    }
}
