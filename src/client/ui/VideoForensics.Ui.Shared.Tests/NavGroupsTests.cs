namespace VideoForensics.Ui.Shared.Tests;

using Xunit;
using VideoForensics.Ui.Shared.Layout;
using VideoForensics.Data.Common.Entities;
using System.Reflection;

public class NavGroups_Tests
{
    [Fact]
    public void All_HasExactlyFiveGroups()
    {
        Assert.Equal(5, NavGroups.All.Count);
    }

    [Fact]
    public void All_HasGroupsInExpectedOrder()
    {
        var groupKeys = NavGroups.All.Select(g => g.Key).ToList();
        Assert.Equal(new[] { "evidence", "cases", "analyze", "sources", "admin" }, groupKeys);
    }

    [Fact]
    public void Evidence_GroupHasCorrectProperties()
    {
        var group = NavGroups.All.First(g => g.Key == "evidence");
        Assert.Equal("Evidence", group.Text);
        Assert.Equal("/evidence", group.Path);

        var itemPaths = group.Items.Select(i => i.Path).ToList();
        Assert.Contains("/evidence", itemPaths);
        Assert.Contains("/events", itemPaths);
        Assert.Contains("/collect/videos", itemPaths);
        Assert.Contains("/collect/snapshots", itemPaths);
    }

    [Fact]
    public void Cases_GroupHasCorrectProperties()
    {
        var group = NavGroups.All.First(g => g.Key == "cases");
        Assert.Equal("Cases", group.Text);
        Assert.Equal("/cases", group.Path);

        var itemPaths = group.Items.Select(i => i.Path).ToList();
        Assert.Contains("/cases", itemPaths);
        Assert.Contains("/cases/new", itemPaths);
        Assert.Contains("/analyze/chain-of-custody", itemPaths);
        Assert.Contains("/analyze/validate/integrity", itemPaths);
        Assert.Contains("/review/export", itemPaths);
    }

    [Fact]
    public void Analyze_GroupHasCorrectProperties()
    {
        var group = NavGroups.All.First(g => g.Key == "analyze");
        Assert.Equal("Analyze", group.Text);
        Assert.Equal("/analyze/reports", group.Path);

        var itemPaths = group.Items.Select(i => i.Path).ToList();
        Assert.Contains("/analyze/reports", itemPaths);
        Assert.Contains("/analyze/signal-anomalies", itemPaths);
        Assert.Contains("/analyze/access-control", itemPaths);
        Assert.Contains("/analyze/jamming", itemPaths);
    }

    [Fact]
    public void Sources_GroupHasCorrectProperties()
    {
        var group = NavGroups.All.First(g => g.Key == "sources");
        Assert.Equal("Sources", group.Text);
        Assert.Equal("/accounts", group.Path);

        var itemPaths = group.Items.Select(i => i.Path).ToList();
        Assert.Contains("/accounts", itemPaths);
        Assert.Contains("/devices/config", itemPaths);
        Assert.Contains("/query", itemPaths);
        Assert.Contains("/tools/ring-selftest", itemPaths);
        Assert.Contains("/tools/import-export", itemPaths);
    }

    [Fact]
    public void Admin_GroupHasCorrectProperties()
    {
        var group = NavGroups.All.First(g => g.Key == "admin");
        Assert.Equal("Admin", group.Text);
        Assert.Equal("/settings", group.Path);

        var itemPaths = group.Items.Select(i => i.Path).ToList();
        Assert.Contains("/settings", itemPaths);
        Assert.Contains("/settings/infrastructure", itemPaths);
        Assert.Contains("/settings/operators", itemPaths);
        Assert.Contains("/settings/devices", itemPaths);
        Assert.Contains("/settings/network", itemPaths);
        Assert.Contains("/settings/storage", itemPaths);
        Assert.Contains("/settings/update-check", itemPaths);
        Assert.Contains("/settings/notifications", itemPaths);
        Assert.Contains("/settings/security-log", itemPaths);
        Assert.Contains("/settings/lockout-policy", itemPaths);
        Assert.Contains("/settings/app-lock", itemPaths);
    }

    [Fact]
    public void CaseNew_RequiresReviewRole()
    {
        var group = NavGroups.All.First(g => g.Key == "cases");
        var newCaseItem = group.Items.First(i => i.Path == "/cases/new");

        // Should NOT be visible to ReadOnly
        var readOnlyCtx = new NavContext(IsSignedIn: true, Role: OperatorRole.ReadOnly, AppLockSupported: false);
        Assert.False(newCaseItem.IsVisible(readOnlyCtx));

        // Should be visible to Review
        var reviewCtx = new NavContext(IsSignedIn: true, Role: OperatorRole.Review, AppLockSupported: false);
        Assert.True(newCaseItem.IsVisible(reviewCtx));
    }

    [Fact]
    public void Operators_RequiresSuperAdminRole()
    {
        var group = NavGroups.All.First(g => g.Key == "admin");
        var operatorsItem = group.Items.First(i => i.Path == "/settings/operators");

        // Should NOT be visible to Admin
        var adminCtx = new NavContext(IsSignedIn: true, Role: OperatorRole.Admin, AppLockSupported: false);
        Assert.False(operatorsItem.IsVisible(adminCtx));

        // Should be visible to SuperAdmin
        var superAdminCtx = new NavContext(IsSignedIn: true, Role: OperatorRole.SuperAdmin, AppLockSupported: false);
        Assert.True(operatorsItem.IsVisible(superAdminCtx));
    }

    [Fact]
    public void SecurityDevices_RequiresSuperAdminRole()
    {
        var group = NavGroups.All.First(g => g.Key == "admin");
        var devicesItem = group.Items.First(i => i.Path == "/settings/devices");

        // Should NOT be visible to Admin
        var adminCtx = new NavContext(IsSignedIn: true, Role: OperatorRole.Admin, AppLockSupported: false);
        Assert.False(devicesItem.IsVisible(adminCtx));

        // Should be visible to SuperAdmin
        var superAdminCtx = new NavContext(IsSignedIn: true, Role: OperatorRole.SuperAdmin, AppLockSupported: false);
        Assert.True(devicesItem.IsVisible(superAdminCtx));
    }

    [Fact]
    public void LockoutPolicy_RequiresSuperAdminRole()
    {
        var group = NavGroups.All.First(g => g.Key == "admin");
        var lockoutPolicyItem = group.Items.First(i => i.Path == "/settings/lockout-policy");

        // Should NOT be visible to Admin
        var adminCtx = new NavContext(IsSignedIn: true, Role: OperatorRole.Admin, AppLockSupported: false);
        Assert.False(lockoutPolicyItem.IsVisible(adminCtx));

        // Should be visible to SuperAdmin
        var superAdminCtx = new NavContext(IsSignedIn: true, Role: OperatorRole.SuperAdmin, AppLockSupported: false);
        Assert.True(lockoutPolicyItem.IsVisible(superAdminCtx));
    }

    [Fact]
    public void Notifications_RequiresAdminRole()
    {
        var group = NavGroups.All.First(g => g.Key == "admin");
        var notificationsItem = group.Items.First(i => i.Path == "/settings/notifications");

        // Should NOT be visible to ReadOnly
        var readOnlyCtx = new NavContext(IsSignedIn: true, Role: OperatorRole.ReadOnly, AppLockSupported: false);
        Assert.False(notificationsItem.IsVisible(readOnlyCtx));

        // Should be visible to Admin
        var adminCtx = new NavContext(IsSignedIn: true, Role: OperatorRole.Admin, AppLockSupported: false);
        Assert.True(notificationsItem.IsVisible(adminCtx));
    }

    [Fact]
    public void AppLock_RequiresAppLockSupport()
    {
        var group = NavGroups.All.First(g => g.Key == "admin");
        var appLockItem = group.Items.First(i => i.Path == "/settings/app-lock");

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
        foreach (var group in NavGroups.All)
        {
            navPaths.Add(group.Path);
            foreach (var item in group.Items)
            {
                navPaths.Add(item.Path);
            }
        }

        var signedInCtx = new NavContext(IsSignedIn: true, Role: OperatorRole.SuperAdmin, AppLockSupported: true);
        var signedOutCtx = new NavContext(IsSignedIn: false, Role: null, AppLockSupported: false);
        foreach (var item in UserMenu.Items(signedInCtx).Concat(UserMenu.Items(signedOutCtx)))
        {
            if (item.Path is not null)
            {
                navPaths.Add(item.Path);
            }
        }

        // Every route actually declared via @page in this assembly (excluding parameterized
        // templates, e.g. "/cases/{id}", which a plain nav path never matches literally).
        var declaredRoutes = CollectRouteTemplates(typeof(NavGroups).Assembly);

        var missingRoutes = navPaths.Where(p => !declaredRoutes.Contains(p)).ToList();
        Assert.Empty(missingRoutes);
    }

    private static HashSet<string> CollectRouteTemplates(Assembly assembly)
    {
        var routes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in assembly.GetTypes())
        {
            foreach (var attribute in type.GetCustomAttributes(typeof(Microsoft.AspNetCore.Components.RouteAttribute), inherit: false))
            {
                var template = ((Microsoft.AspNetCore.Components.RouteAttribute)attribute).Template;
                if (!template.Contains('{'))
                {
                    routes.Add(template);
                }
            }
        }

        return routes;
    }
}
