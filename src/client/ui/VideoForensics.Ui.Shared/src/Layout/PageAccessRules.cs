using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Ui.Shared.Layout
{
    /// <summary>
    /// Access policies a page can require. Names match WebApp's <c>VideoForensicsPolicies</c>; keep the two in step.
    /// </summary>
    public enum AccessPolicy
    {
        ReadOnly,
        Review,
        Admin,
        SuperAdmin,

        /// <summary>SuperAdmin on the Local network tier only - a passkey on a remote tier does not satisfy it.</summary>
        SuperAdminLocal,

        /// <summary>SuperAdmin on the Local tier, or any tier with an operator-passkey session.</summary>
        SuperAdminLocalOrPasskey,
    }

    /// <summary>
    /// Single source of truth for which operators may open a page. Nav visibility (<see cref="NavItem.IsVisible"/>)
    /// and the route guard (<c>PageAccessRouteView</c>) both read this table, so a page cannot be hidden in the
    /// sidebar yet reachable by URL, or the reverse. Pages not listed here stay open to any signed-in operator.
    /// The server remains authoritative; this table only keeps the UI from offering pages the API will refuse.
    /// </summary>
    public static class PageAccessRules
    {
        private static readonly IReadOnlyDictionary<string, AccessPolicy> Rules =
            new Dictionary<string, AccessPolicy>(StringComparer.OrdinalIgnoreCase)
            {
                ["/chat"] = AccessPolicy.ReadOnly,
                ["/cases/new"] = AccessPolicy.Review,
                ["/settings/security-events"] = AccessPolicy.ReadOnly,
                ["/settings/operators"] = AccessPolicy.SuperAdmin,
                ["/admin/pending-credentials"] = AccessPolicy.SuperAdmin,
                ["/settings/infrastructure"] = AccessPolicy.Admin,
                ["/tools/import-export"] = AccessPolicy.Admin,
                ["/settings/notifications"] = AccessPolicy.Admin,
                ["/settings/security-log"] = AccessPolicy.Admin,
                ["/settings/devices"] = AccessPolicy.SuperAdminLocal,
                ["/settings/network"] = AccessPolicy.SuperAdminLocal,
                ["/settings/storage"] = AccessPolicy.SuperAdminLocal,
                ["/settings/update-check"] = AccessPolicy.SuperAdminLocal,
                ["/settings/logs"] = AccessPolicy.SuperAdminLocal,
                ["/settings/llm"] = AccessPolicy.SuperAdmin,
                ["/settings/lockout-policy"] = AccessPolicy.SuperAdminLocal,
                ["/settings/auth-methods"] = AccessPolicy.SuperAdminLocal,
            };

        /// <summary>Returns the policy a page requires, if the page is listed. Query strings are ignored.</summary>
        public static bool TryGetPolicy(string path, out AccessPolicy policy)
        {
            return Rules.TryGetValue(StripQuery(path), out policy);
        }

        /// <summary>True when the operator in <paramref name="ctx"/> may open <paramref name="path"/>.</summary>
        public static bool IsAllowed(string path, NavContext ctx)
        {
            return !TryGetPolicy(path, out AccessPolicy policy) || Allows(policy, ctx);
        }

        public static bool Allows(AccessPolicy policy, NavContext ctx)
        {
            return policy switch
            {
                AccessPolicy.ReadOnly => ctx.HasRole(OperatorRole.ReadOnly),
                AccessPolicy.Review => ctx.HasRole(OperatorRole.Review),
                AccessPolicy.Admin => ctx.HasRole(OperatorRole.Admin),
                AccessPolicy.SuperAdmin => ctx.HasRole(OperatorRole.SuperAdmin),
                AccessPolicy.SuperAdminLocal => ctx.HasRole(OperatorRole.SuperAdmin) && ctx.IsLocalTier,
                AccessPolicy.SuperAdminLocalOrPasskey => ctx.HasRole(OperatorRole.SuperAdmin) && (ctx.IsLocalTier || ctx.IsPasskeySession),
                _ => false,
            };
        }

        private static string StripQuery(string path)
        {
            int queryStart = path.IndexOf('?');
            return queryStart < 0 ? path : path[..queryStart];
        }
    }
}
