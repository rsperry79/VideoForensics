using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Ui.Shared.Layout
{
    /// <summary>A single left-nav item within a top-level tab group.</summary>
    public sealed record NavItem(string Text, string Path, Func<NavContext, bool>? Visible = null)
    {
        public bool IsVisible(NavContext ctx)
        {
            return Visible is null || Visible(ctx);
        }
    }

    /// <summary>A top-level tab, and the left-nav items shown while it's active.</summary>
    public sealed record NavGroup(string Key, string Text, string Path, IReadOnlyList<NavItem> Items, Func<NavContext, bool>? Visible = null)
    {
        public bool IsVisible(NavContext ctx)
        {
            return Visible is null || Visible(ctx);
        }
    }

    /// <summary>Role/session state needed to evaluate a NavItem/NavGroup's visibility predicate.</summary>
    public sealed record NavContext(bool IsSignedIn, OperatorRole? Role, bool AppLockSupported)
    {
        public bool HasRole(OperatorRole minimum)
        {
            return IsSignedIn && Role is not null && Role >= minimum;
        }
    }

    /// <summary>
    /// The full nav tree - single source of truth for both the top tab bar (groups) and the left
    /// vertical tab rail (the active group's items). Replaces the inline RadzenMenuItem tree that
    /// used to live directly in MainLayout.razor's markup.
    /// </summary>
    public static class NavGroups
    {
        public static readonly IReadOnlyList<NavGroup> All =
        [
            new("evidence", "Evidence", "/evidence",
            [
                new("Evidence", "/evidence"),
                new("Event Grid", "/events"),
                new("Collect Videos", "/collect/videos"),
                new("Collect Snapshots", "/collect/snapshots")
            ]),

            new("cases", "Cases", "/cases",
            [
                new("All Cases", "/cases"),
                new("New Case", "/cases/new", ctx => ctx.HasRole(OperatorRole.Review)),
                new("Chain of Custody", "/analyze/chain-of-custody"),
                new("Validate Evidence", "/analyze/validate/integrity"),
                new("Export Evidence", "/review/export")
            ]),

            new("analyze", "Analyze", "/analyze",
            [
                new("Forensic Reports", "/analyze?analysis=reports"),
                new("Signal Anomalies", "/analyze?analysis=anomalies"),
                new("Access Control", "/analyze?analysis=access"),
                new("Jamming Analysis", "/analyze?analysis=jamming")
            ]),

            new("sources", "Sources", "/accounts",
            [
                new("Provider Accounts", "/accounts"),
                new("Device Configuration", "/devices/config"),
                new("Query API", "/query"),
                new("API Tester", "/tools/ring-selftest"),
                new("Import / Export", "/tools/import-export")
            ]),

            new("admin", "Admin", "/settings",
            [
                new("General", "/settings"),
                new("Infrastructure", "/settings/infrastructure"),
                new("Operators", "/settings/operators", ctx => ctx.HasRole(OperatorRole.SuperAdmin)),
                new("Paired Devices", "/settings/devices", ctx => ctx.HasRole(OperatorRole.SuperAdmin)),
                new("Network Access", "/settings/network", ctx => ctx.HasRole(OperatorRole.SuperAdmin)),
                new("Storage", "/settings/storage", ctx => ctx.HasRole(OperatorRole.SuperAdmin)),
                new("App Update", "/settings/update-check", ctx => ctx.HasRole(OperatorRole.SuperAdmin)),
                new("Notifications", "/settings/notifications", ctx => ctx.HasRole(OperatorRole.Admin)),
                new("Security Audit Log", "/settings/security-log", ctx => ctx.HasRole(OperatorRole.Admin)),
                // Mirrors SecurityLockoutPolicy's own backend policy: /api/v1/lockout-policy is
                // mapped behind VideoForensicsPolicies.SuperAdminLocal (see LockoutPolicyEndpoints.cs).
                new("Lockout Policy", "/settings/lockout-policy", ctx => ctx.HasRole(OperatorRole.SuperAdmin)),
                new("App Lock", "/settings/app-lock", ctx => ctx.AppLockSupported)
            ])
        ];
    }

    /// <summary>
    /// Menu items for the operator's own login (displayed in top bar).
    /// Separate from provider accounts - this is for changing the current user's password, passkeys, and signing out.
    /// </summary>
    public static class UserMenu
    {
        public sealed record UserMenuItem(string Text, string? Path = null, Func<NavContext, bool>? Visible = null)
        {
            public bool IsVisible(NavContext ctx)
            {
                return Visible is null || Visible(ctx);
            }
        }

        public static IReadOnlyList<UserMenuItem> Items(NavContext ctx)
        {
            var items = new List<UserMenuItem>();

            if (ctx.IsSignedIn)
            {
                items.Add(new("Change Password", "/change-password"));
                items.Add(new("My Passkeys", "/settings/passkeys"));
                items.Add(new("Sign Out", null)); // No path - handled by click handler
            }
            else
            {
                items.Add(new("Device Sign-In", "/device-signin"));
            }

            return items;
        }
    }
}
