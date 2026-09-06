using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Ui.Shared.Layout
{
    /// <summary>A single left-nav item within a top-level tab group.</summary>
    public sealed record NavItem(string Text, string Path, Func<NavContext, bool>? Visible = null)
    {
        public bool IsVisible(NavContext ctx) => Visible is null || Visible(ctx);
    }

    /// <summary>A top-level tab, and the left-nav items shown while it's active.</summary>
    public sealed record NavGroup(string Key, string Text, string Path, IReadOnlyList<NavItem> Items, Func<NavContext, bool>? Visible = null)
    {
        public bool IsVisible(NavContext ctx) => Visible is null || Visible(ctx);
    }

    /// <summary>Role/session state needed to evaluate a NavItem/NavGroup's visibility predicate.</summary>
    public sealed record NavContext(bool IsSignedIn, OperatorRole? Role, bool AppLockSupported)
    {
        public bool HasRole(OperatorRole minimum) => IsSignedIn && Role is not null && Role >= minimum;
    }

    /// <summary>
    /// The full nav tree - single source of truth for both the top tab bar (groups) and the left
    /// vertical tab rail (the active group's items). Replaces the inline RadzenMenuItem tree that
    /// used to live directly in MainLayout.razor's markup.
    /// </summary>
    public static class NavGroups
    {
        public static readonly IReadOnlyList<NavGroup> All = new List<NavGroup>
        {
            new("dashboard", "Dashboard", "/", new List<NavItem>
            {
                new("Dashboard", "/"),
                new("Full Workflow", "/workflow")
            }),

            new("collect", "Collect", "/collect/videos", new List<NavItem>
            {
                new("Collect Videos", "/collect/videos"),
                new("Collect Snapshots", "/collect/snapshots")
            }),

            new("analyze", "Analyze", "/analyze/reports", new List<NavItem>
            {
                new("Forensic Reports", "/analyze/reports"),
                new("Signal Anomalies", "/analyze/signal-anomalies"),
                new("Access Control", "/analyze/access-control"),
                new("Chain of Custody", "/analyze/chain-of-custody"),
                new("Validate Evidence", "/analyze/validate/integrity"),
                new("Jamming Analysis", "/analyze/jamming")
            }),

            new("events", "Events", "/events", new List<NavItem>
            {
                new("Events", "/events")
            }),

            new("devices", "Devices", "/devices/config", new List<NavItem>
            {
                new("Device Configuration", "/devices/config"),
                new("Paired Devices", "/settings/devices", ctx => ctx.HasRole(OperatorRole.SuperAdmin)),
                new("Device Sign-In", "/device-signin", ctx => !ctx.IsSignedIn)
            }),

            new("tools", "Tools", "/query", new List<NavItem>
            {
                new("Query API", "/query"),
                new("Import / Export", "/tools/import-export")
            }),

            new("settings", "Settings", "/settings", new List<NavItem>
            {
                new("General", "/settings"),
                new("Accounts", "/accounts"),
                new("Infrastructure", "/settings/infrastructure"),
                new("Operators", "/settings/operators", ctx => ctx.HasRole(OperatorRole.SuperAdmin)),
                new("Network Access", "/settings/network", ctx => ctx.HasRole(OperatorRole.SuperAdmin)),
                new("Remote Access", "/settings/remote-access", ctx => ctx.HasRole(OperatorRole.SuperAdmin)),
                new("Notifications", "/settings/notifications", ctx => ctx.HasRole(OperatorRole.Admin)),
                new("Security Audit Log", "/settings/security-log", ctx => ctx.HasRole(OperatorRole.Admin)),
                new("App Lock", "/settings/app-lock", ctx => ctx.AppLockSupported)
            })
        };
    }
}
