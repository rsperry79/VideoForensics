
using System.Reflection;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Ui.Shared.Layout;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Layout;

public class NavGroups_Tests
{
    [Fact]
    public void All_HasExactlyThreeGroups()
    {
        Assert.Equal(3, NavGroups.All.Count);
    }

    [Fact]
    public void All_HasGroupsInExpectedOrder()
    {
        var groupKeys = NavGroups.All.Select(g => g.Key).ToList();
        Assert.Equal(new[] { "media", "cases", "admin" }, groupKeys);
    }

    [Fact]
    public void Media_GroupHasCorrectProperties()
    {
        NavGroup group = NavGroups.All.First(g => g.Key == "media");
        Assert.Equal("Media", group.Text);
        Assert.Equal("/media", group.Path);

        // Media has no sub menu: Signal history and Chat moved under Cases.
        Assert.Equal(new[] { "/media" }, group.Items.Select(i => i.Path).ToList());
    }

    [Fact]
    public void Media_GroupNoLongerHasEventGrid()
    {
        // The Events page ("Event Grid") was retired in phase 8: its actions moved to Media's
        // Grid context menu and Inspector (see EvidenceActions/EvidenceInspectorMapper), and the
        // old /events route now just redirects (see EventsRedirect).
        NavGroup group = NavGroups.All.First(g => g.Key == "media");
        var itemPaths = group.Items.Select(i => i.Path).ToList();
        Assert.DoesNotContain("/events", itemPaths);
        Assert.DoesNotContain(group.Items, i => i.Text == "Event Grid");
    }

    [Fact]
    public void Cases_GroupHasCorrectProperties()
    {
        NavGroup group = NavGroups.All.First(g => g.Key == "cases");
        Assert.Equal("Review", group.Text);
        Assert.Equal("/cases", group.Path);

        var itemPaths = group.Items.Select(i => i.Path).ToList();
        Assert.Contains("/cases", itemPaths);
        Assert.Contains("/signal-history", itemPaths);
        Assert.Contains("/chat", itemPaths);
        Assert.Contains("/analyze", itemPaths);
        Assert.Contains("/analyze/chain-of-custody", itemPaths);
        Assert.DoesNotContain("/analyze/validate/integrity", itemPaths);
        Assert.Contains("/review/export", itemPaths);

        // Verify Analyze is nested under Cases with its sub-items
        NavItem? analyzeItem = group.Items.FirstOrDefault(i => i.Path == "/analyze");
        Assert.NotNull(analyzeItem);
        Assert.NotNull(analyzeItem.Children);
        var analyzeChildPaths = analyzeItem.Children.Select(c => c.Path).ToList();
        Assert.Contains("/analyze?analysis=reports", analyzeChildPaths);
        Assert.Contains("/analyze?analysis=anomalies", analyzeChildPaths);
        Assert.Contains("/analyze?analysis=access", analyzeChildPaths);
        Assert.Contains("/analyze?analysis=jamming", analyzeChildPaths);
    }

    [Fact]
    public void Analyze_NoLongerInMedia_NestedUnderCases()
    {
        // Analyze moved from the Media group to be nested under Cases.
        // Verify it doesn't exist as a top-level group anymore.
        NavGroup? analyzeGroup = NavGroups.All.FirstOrDefault(g => g.Key == "analyze");
        Assert.Null(analyzeGroup);

        // Verify it exists as a nested item under Cases instead
        NavGroup casesGroup = NavGroups.All.First(g => g.Key == "cases");
        NavItem analyzeItem = casesGroup.Items.First(i => i.Path == "/analyze");
        Assert.Equal("Analyze", analyzeItem.Text);
        Assert.NotNull(analyzeItem.Children);
    }

    [Fact]
    public void Sources_ItemsLiveUnderAdminInSourcesSection()
    {
        Assert.DoesNotContain(NavGroups.All, g => g.Key == "sources");

        NavGroup group = NavGroups.All.First(g => g.Key == "admin");
        var sourceItems = group.Items.Where(i => i.Section == "Sources").ToList();
        Assert.Equal(new[] { "/accounts", "/query" }, sourceItems.Select(i => i.Path).ToList());
        Assert.DoesNotContain(group.Items, i => i.Path == "/query/api-tester" || i.Path == "/tools/ring-selftest" || i.Path == "/devices/config");
    }

    [Fact]
    public void Sources_QueryApi_RequiresAdminRole()
    {
        NavGroup group = NavGroups.All.First(g => g.Key == "admin");
        NavItem queryItem = group.Items.First(i => i.Path == "/query");

        Assert.False(queryItem.IsVisible(new NavContext(IsSignedIn: true, Role: OperatorRole.Review, AppLockSupported: false)));
        Assert.True(queryItem.IsVisible(new NavContext(IsSignedIn: true, Role: OperatorRole.Admin, AppLockSupported: false)));
    }

    [Fact]
    public void Sources_ProviderAccounts_VisibleToAdminAndUp_HiddenFromReview()
    {
        NavGroup group = NavGroups.All.First(g => g.Key == "admin");
        NavItem accountsItem = group.Items.First(i => i.Path == "/accounts");

        Assert.False(accountsItem.IsVisible(new NavContext(IsSignedIn: true, Role: OperatorRole.Review, AppLockSupported: false)));
        Assert.True(accountsItem.IsVisible(new NavContext(IsSignedIn: true, Role: OperatorRole.Admin, AppLockSupported: false)));
    }

    [Fact]
    public void Permissions_UnderAdminSecurity_RequiresLocalOrPasskeySuperAdmin()
    {
        NavGroup group = NavGroups.All.First(g => g.Key == "admin");
        NavItem permissionsItem = group.Items.First(i => i.Path == "/settings/permissions");

        Assert.Equal("Security", permissionsItem.Section);
        Assert.False(permissionsItem.IsVisible(new NavContext(IsSignedIn: true, Role: OperatorRole.Admin, AppLockSupported: false)));
        Assert.False(permissionsItem.IsVisible(new NavContext(IsSignedIn: true, Role: OperatorRole.SuperAdmin, AppLockSupported: false)));
        Assert.True(permissionsItem.IsVisible(new NavContext(IsSignedIn: true, Role: OperatorRole.SuperAdmin, AppLockSupported: false, IsLocalTier: true)));
        Assert.True(permissionsItem.IsVisible(new NavContext(IsSignedIn: true, Role: OperatorRole.SuperAdmin, AppLockSupported: false, IsPasskeySession: true)));
    }

    [Fact]
    public void Admin_GroupHasCorrectProperties()
    {
        NavGroup group = NavGroups.All.First(g => g.Key == "admin");
        Assert.Equal("Admin", group.Text);
        Assert.Equal("/settings", group.Path);

        var itemPaths = group.Items.Select(i => i.Path).ToList();
        Assert.Contains("/settings", itemPaths);
        Assert.Contains("/settings/infrastructure", itemPaths);
        Assert.Contains("/settings/operators", itemPaths);
        Assert.Contains("/settings/network", itemPaths);
        Assert.Contains("/settings/storage", itemPaths);
        Assert.Contains("/settings/update-check", itemPaths);
        Assert.Contains("/settings/notifications", itemPaths);
        Assert.Contains("/settings/security-log", itemPaths);
        Assert.Contains("/settings/lockout-policy", itemPaths);
        Assert.Contains("/settings/auth-methods", itemPaths);
        Assert.Contains("/settings/app-lock", itemPaths);
    }

    [Fact]
    public void CaseNew_IsNotInTheMenu()
    {
        // A manual case starts from Signal history, not from the menu.
        Assert.DoesNotContain(NavGroups.All.SelectMany(g => g.Items), i => i.Path == "/cases/new");
    }

    [Fact]
    public void ServerLogs_RequiresSuperAdminRole()
    {
        NavGroup group = NavGroups.All.First(g => g.Key == "admin");
        NavItem logsItem = group.Items.First(i => i.Path == "/settings/logs");

        Assert.Equal("Server Logs", logsItem.Text);
        Assert.True(logsItem.IsVisible(new NavContext(IsSignedIn: true, Role: OperatorRole.SuperAdmin, AppLockSupported: false, IsLocalTier: true)));
        Assert.False(logsItem.IsVisible(new NavContext(IsSignedIn: true, Role: OperatorRole.SuperAdmin, AppLockSupported: false)));
        Assert.False(logsItem.IsVisible(new NavContext(IsSignedIn: true, Role: OperatorRole.Admin, AppLockSupported: false)));
        Assert.False(logsItem.IsVisible(new NavContext(IsSignedIn: true, Role: OperatorRole.Review, AppLockSupported: false)));
        Assert.False(logsItem.IsVisible(new NavContext(IsSignedIn: true, Role: OperatorRole.ReadOnly, AppLockSupported: false)));
        Assert.False(logsItem.IsVisible(new NavContext(IsSignedIn: false, Role: null, AppLockSupported: false)));
    }

    [Fact]
    public void ServerLogs_SitsImmediatelyAfterSecurityAuditLog()
    {
        var items = NavGroups.All.First(g => g.Key == "admin").Items.ToList();

        int auditIndex = items.FindIndex(i => i.Text == "Security Audit Log");
        Assert.True(auditIndex >= 0);
        Assert.Equal("Server Logs", items[auditIndex + 1].Text);
    }
    [Fact]
    public void Operators_RequiresSuperAdminRole()
    {
        NavGroup group = NavGroups.All.First(g => g.Key == "admin");
        NavItem operatorsItem = group.Items.First(i => i.Path == "/settings/operators");

        // Should NOT be visible to Admin
        var adminCtx = new NavContext(IsSignedIn: true, Role: OperatorRole.Admin, AppLockSupported: false);
        Assert.False(operatorsItem.IsVisible(adminCtx));

        // Should be visible to SuperAdmin on the Local tier (the operator endpoints are SuperAdminLocal)
        var superAdminCtx = new NavContext(IsSignedIn: true, Role: OperatorRole.SuperAdmin, AppLockSupported: false, IsLocalTier: true);
        Assert.True(operatorsItem.IsVisible(superAdminCtx));
    }

    [Fact]
    public void PairedDevices_IsNotInTheMenu()
    {
        // Paired devices are not used; the page and its route remain for the links that still point at it.
        Assert.DoesNotContain(NavGroups.All.SelectMany(g => g.Items), i => i.Path == "/settings/devices");
    }

    [Fact]
    public void LockoutPolicy_RequiresSuperAdminRole()
    {
        NavGroup group = NavGroups.All.First(g => g.Key == "admin");
        NavItem lockoutPolicyItem = group.Items.First(i => i.Path == "/settings/lockout-policy");

        // Should NOT be visible to Admin
        var adminCtx = new NavContext(IsSignedIn: true, Role: OperatorRole.Admin, AppLockSupported: false);
        Assert.False(lockoutPolicyItem.IsVisible(adminCtx));

        // Should be visible to SuperAdmin on the Local tier
        var superAdminCtx = new NavContext(IsSignedIn: true, Role: OperatorRole.SuperAdmin, AppLockSupported: false, IsLocalTier: true);
        Assert.True(lockoutPolicyItem.IsVisible(superAdminCtx));
    }

    [Fact]
    public void LoginMethods_RequiresSuperAdminRole()
    {
        NavGroup group = NavGroups.All.First(g => g.Key == "admin");
        NavItem loginMethodsItem = group.Items.First(i => i.Path == "/settings/auth-methods");

        // Should NOT be visible to Admin
        var adminCtx = new NavContext(IsSignedIn: true, Role: OperatorRole.Admin, AppLockSupported: false);
        Assert.False(loginMethodsItem.IsVisible(adminCtx));

        // Should be visible to SuperAdmin on the Local tier
        var superAdminCtx = new NavContext(IsSignedIn: true, Role: OperatorRole.SuperAdmin, AppLockSupported: false, IsLocalTier: true);
        Assert.True(loginMethodsItem.IsVisible(superAdminCtx));
    }

    [Fact]
    public void Notifications_DefaultsToSuperAdmin_AdminOnlyWhenOpened()
    {
        NavGroup group = NavGroups.All.First(g => g.Key == "admin");
        NavItem notificationsItem = group.Items.First(i => i.Path == "/settings/notifications");

        // Should NOT be visible to ReadOnly
        var readOnlyCtx = new NavContext(IsSignedIn: true, Role: OperatorRole.ReadOnly, AppLockSupported: false);
        Assert.False(notificationsItem.IsVisible(readOnlyCtx));

        // Hidden from Admin until a Super Admin opens notification settings to admins
        var adminCtx = new NavContext(IsSignedIn: true, Role: OperatorRole.Admin, AppLockSupported: false);
        Assert.False(notificationsItem.IsVisible(adminCtx));
        Assert.True(notificationsItem.IsVisible(adminCtx with { NotificationsOpenToAdmin = true }));

        // Super Admin always sees it
        Assert.True(notificationsItem.IsVisible(new NavContext(IsSignedIn: true, Role: OperatorRole.SuperAdmin, AppLockSupported: false)));
    }

    [Fact]
    public void AppLock_RequiresAppLockSupport()
    {
        NavGroup group = NavGroups.All.First(g => g.Key == "admin");
        NavItem appLockItem = group.Items.First(i => i.Path == "/settings/app-lock");

        // Should NOT be visible if AppLock not supported
        var noLockCtx = new NavContext(IsSignedIn: true, Role: OperatorRole.Admin, AppLockSupported: false);
        Assert.False(appLockItem.IsVisible(noLockCtx));

        // Should be visible if AppLock is supported
        var withLockCtx = new NavContext(IsSignedIn: true, Role: OperatorRole.Admin, AppLockSupported: true);
        Assert.True(appLockItem.IsVisible(withLockCtx));
    }

    [Fact]
    public void AllNavAndUserMenuPaths_ExistAsPageRoutes()
    {
        // Collect every path NavGroups and UserMenu can produce (deduplicated). UserMenu.Items()
        // depends on NavContext, so both the signed-in and signed-out shapes are unioned; "Sign
        // Out" has no Path (it's a click handler, not a route) and is naturally skipped.
        var navPaths = new HashSet<string>();
        foreach (NavGroup group in NavGroups.All)
        {
            // Extract path portion before query string
            string groupPath = group.Path.Split('?')[0];
            _ = navPaths.Add(groupPath);
            foreach (NavItem item in group.Items)
            {
                // Extract path portion before query string
                string itemPath = item.Path.Split('?')[0];
                _ = navPaths.Add(itemPath);
                // Also check nested children
                if (item.Children != null)
                {
                    foreach (NavItem child in item.Children)
                    {
                        string childPath = child.Path.Split('?')[0];
                        _ = navPaths.Add(childPath);
                    }
                }
            }
        }

        var signedInCtx = new NavContext(IsSignedIn: true, Role: OperatorRole.SuperAdmin, AppLockSupported: true);
        var signedOutCtx = new NavContext(IsSignedIn: false, Role: null, AppLockSupported: false);
        foreach (UserMenu.UserMenuItem? item in UserMenu.Items(signedInCtx).Concat(UserMenu.Items(signedOutCtx)))
        {
            if (item.Path is not null)
            {
                // Extract path portion before query string
                string path = item.Path.Split('?')[0];
                _ = navPaths.Add(path);
            }
        }

        // Every route actually declared via @page in this assembly (excluding parameterized
        // templates, e.g. "/cases/{id}", which a plain nav path never matches literally).
        HashSet<string> declaredRoutes = CollectRouteTemplates(typeof(NavGroups).Assembly);

        var missingRoutes = navPaths.Where(p => !declaredRoutes.Contains(p)).ToList();
        Assert.Empty(missingRoutes);
    }

    private static HashSet<string> CollectRouteTemplates(Assembly assembly)
    {
        var routes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Type type in assembly.GetTypes())
        {
            foreach (object attribute in type.GetCustomAttributes(typeof(Microsoft.AspNetCore.Components.RouteAttribute), inherit: false))
            {
                string template = ((Microsoft.AspNetCore.Components.RouteAttribute)attribute).Template;
                if (!template.Contains('{'))
                {
                    _ = routes.Add(template);
                }
            }
        }

        return routes;
    }
}