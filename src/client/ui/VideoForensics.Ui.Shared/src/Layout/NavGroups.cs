using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Ui.Shared.Services;

namespace VideoForensics.Client.Ui.Shared.Layout
{
    /// <summary>
    /// A single left-nav item within a top-level tab group. Who may see it comes from <see cref="PageAccessRules"/>;
    /// <paramref name="Visible"/> is only for extra, non-role conditions (e.g. feature support).
    /// <paramref name="Section"/> files the item under a collapsible sidebar heading; null means no heading.
    /// </summary>
    public sealed record NavItem(string Text, string Path, Func<NavContext, bool>? Visible = null, IReadOnlyList<NavItem>? Children = null, string? Section = null)
    {
        /// <summary>True when the item should appear for <paramref name="ctx"/>: the page access rule and any extra predicate both pass.</summary>
        public bool IsVisible(NavContext ctx)
        {
            return PageAccessRules.IsAllowed(Path, ctx) && (Visible is null || Visible(ctx));
        }
    }

    /// <summary>
    /// A top-level tab and the left-nav items shown while it's active. A tab with no explicit
    /// <paramref name="Visible"/> predicate is shown when at least one of its items is visible.
    /// </summary>
    public sealed record NavGroup(string Key, string Text, string Path, IReadOnlyList<NavItem> Items, Func<NavContext, bool>? Visible = null)
    {
        /// <summary>True when the tab should appear for <paramref name="ctx"/>.</summary>
        public bool IsVisible(NavContext ctx)
        {
            return Visible is null ? Items.Any(i => i.IsVisible(ctx)) : Visible(ctx);
        }
    }

    /// <summary>A run of visible items under one sidebar heading. A null <see cref="Title"/> is an unheaded run.</summary>
    public sealed record NavSection(string? Title, IReadOnlyList<NavItem> Items);

    /// <summary>Role/session state needed to evaluate nav and page access.</summary>
    public sealed record NavContext(bool IsSignedIn, OperatorRole? Role, bool AppLockSupported, string UiMode = "Standard", bool UiModeLocked = false, bool IsLocalTier = false, bool IsPasskeySession = false)
    {
        /// <summary>Builds the access context for the current circuit's session, using the server-reported tier.</summary>
        public static NavContext FromSession(PairedSessionState session, bool appLockSupported)
        {
            OperatorRole? role = session.Role is not null && Enum.TryParse<OperatorRole>(session.Role, out OperatorRole parsed)
                ? parsed
                : null;

            return new NavContext(
                session.IsSignedIn,
                role,
                appLockSupported,
                IsLocalTier: session.IsLocalTier,
                IsPasskeySession: session.IsPasskeySession);
        }

        /// <summary>True when signed in with a role at or above <paramref name="minimum"/>.</summary>
        public bool HasRole(OperatorRole minimum)
        {
            return IsSignedIn && Role is not null && Role >= minimum;
        }
    }

    /// <summary>
    /// The full nav tree - single source of truth for both the top tab bar (groups) and the left
    /// vertical tab rail (the active group's items). Replaces the inline menu tree that
    /// used to live directly in MainLayout.razor's markup.
    /// </summary>
    public static class NavGroups
    {
        public static readonly IReadOnlyList<NavGroup> All =
        [
            new("media", "Media", "/media",
            [
                new("Media", "/media"),
                new("Chat Assistant", "/chat")
            ]),

            new("cases", "Cases", "/cases",
            [
                new("All Cases", "/cases"),
                new("New Case", "/cases/new"),
                new("Analyze", "/analyze",
                    Children:
                    [
                        new("Forensic Reports", "/analyze?analysis=reports"),
                        new("Signal Anomalies", "/analyze?analysis=anomalies"),
                        new("Access Control", "/analyze?analysis=access"),
                        new("Jamming Analysis", "/analyze?analysis=jamming")
                    ]),
                new("Chain of Custody", "/analyze/chain-of-custody"),
                new("Export Evidence", "/review/export")
            ]),

            new("sources", "Sources", "/accounts",
            [
                new("Provider Accounts", "/accounts"),
                new("Query API", "/query"),
                new("API Tester", "/query/api-tester")
            ]),

            new("admin", "Admin", "/settings",
            [
                new("General", "/settings", Section: "General"),
                new("App Lock", "/settings/app-lock", ctx => ctx.AppLockSupported, Section: "General"),
                new("Notifications", "/settings/notifications", Section: "General"),

                new("Security Events", "/settings/security-events", Section: "Security"),
                new("Operators", "/settings/operators", Section: "Security"),
                new("Pending Approvals", "/admin/pending-credentials", Section: "Security"),
                new("Login Methods", "/settings/auth-methods", Section: "Security"),
                new("Lockout Policy", "/settings/lockout-policy", Section: "Security"),
                new("Security Audit Log", "/settings/security-log", Section: "Security"),

                new("Server Logs", "/settings/logs", Section: "Diagnostics & Updates"),
                new("App Update", "/settings/update-check", Section: "Diagnostics & Updates"),

                new("Paired Devices", "/settings/devices", Section: "Devices & Network"),
                new("Network Access", "/settings/network", Section: "Devices & Network"),
                new("Software Paths", "/settings/software-paths", Section: "Devices & Network"),
                new("Infrastructure", "/settings/infrastructure", Section: "Devices & Network"),

                new("Storage", "/settings/storage", Section: "Storage & Data"),
                new("Import / Export", "/tools/import-export", Section: "Storage & Data"),

                new("LLM API", "/settings/llm", Section: "Integrations"),
            ])
        ];

        /// <summary>
        /// Picks the top tab to highlight for the current location: the group owning the visible item whose <c>Path</c>
        /// is the longest match (most specific), so <c>/analyze/chain-of-custody</c> selects Cases over Evidence's broader
        /// <c>/analyze</c>. Ties keep the earlier group. Falls back to the first visible group, then the first group overall.
        /// </summary>
        /// <param name="visible">The groups visible to the operator, in display order.</param>
        /// <param name="ctx">The operator's access context, used to skip hidden items.</param>
        /// <param name="currentPath">The current URI's path only, e.g. <c>/analyze</c>.</param>
        /// <param name="currentQuery">The current URI's query string, with or without a leading '?'. May be null/empty.</param>
        public static NavGroup ActiveGroupFor(IReadOnlyList<NavGroup> visible, NavContext ctx, string currentPath, string? currentQuery)
        {
            NavGroup? best = null;
            int bestLength = -1;

            foreach (NavGroup group in visible)
            {
                foreach (NavItem item in group.Items)
                {
                    if (item.Path.Length > bestLength && item.IsVisible(ctx) && NavPathMatcher.Matches(currentPath, currentQuery, item.Path))
                    {
                        best = group;
                        bestLength = item.Path.Length;
                    }
                }
            }

            return best ?? visible.FirstOrDefault() ?? All[0];
        }

        /// <summary>
        /// Groups a tab's visible items by <see cref="NavItem.Section"/>, in order of first appearance. Items that share a
        /// heading are merged even when other items sit between them, so each heading appears once. Hidden items are
        /// dropped, so a heading whose items are all hidden for this operator never appears.
        /// </summary>
        public static IReadOnlyList<NavSection> SectionsFor(NavGroup group, NavContext ctx)
        {
            var order = new List<string?>();
            var itemsByKey = new Dictionary<string, List<NavItem>>(StringComparer.Ordinal);

            foreach (NavItem item in group.Items)
            {
                if (!item.IsVisible(ctx))
                {
                    continue;
                }

                // Unheaded items share one run keyed by the empty string; headings are never empty.
                string key = item.Section ?? string.Empty;
                if (!itemsByKey.TryGetValue(key, out List<NavItem>? run))
                {
                    run = [];
                    itemsByKey[key] = run;
                    order.Add(item.Section);
                }

                run.Add(item);
            }

            return order.Select(title => new NavSection(title, itemsByKey[title ?? string.Empty])).ToList();
        }
    }

    /// <summary>
    /// Menu items for the operator's own login (displayed in top bar).
    /// Separate from provider accounts - this is for changing the current user's password, passkeys, and signing out.
    /// </summary>
    public static class UserMenu
    {
        public sealed record UserMenuItem(string Text, string? Path = null, Func<NavContext, bool>? Visible = null, string? Action = null)
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

                // Add UI mode toggle if not locked
                if (!ctx.UiModeLocked)
                {
                    if (ctx.UiMode == "Standard")
                    {
                        items.Add(new("Switch to Simple view", Action: "set-ui-mode:Simple"));
                    }
                    else
                    {
                        items.Add(new("Switch to Standard view", Action: "set-ui-mode:Standard"));
                    }
                }

                items.Add(new("Sign Out", null)); // No path - handled by click handler
            }
            else
            {
                items.Add(new("Sign In", "/signin"));
            }

            return items;
        }
    }
}