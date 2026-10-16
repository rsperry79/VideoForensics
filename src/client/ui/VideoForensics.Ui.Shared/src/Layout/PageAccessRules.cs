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
                // Mirrors /api/v1/accounts (AccountEndpoints) and the device-config API, both SuperAdminLocal. On the WebApp
                // these pages call repositories in-process, so this row is the only gate there.
                ["/accounts"] = AccessPolicy.SuperAdminLocal,
                ["/devices/config"] = AccessPolicy.SuperAdminLocal,
                // Analyze tabs are query-keyed, so each tab has its own row. The first three read the server database
                // under the paired session (Analyze.razor); jamming can change incidents and recompute stats, so Admin.
                ["/analyze?analysis=reports"] = AccessPolicy.ReadOnly,
                ["/analyze?analysis=anomalies"] = AccessPolicy.ReadOnly,
                ["/analyze?analysis=access"] = AccessPolicy.ReadOnly,
                ["/analyze?analysis=jamming"] = AccessPolicy.Admin,
                // Query API calls provider services directly with the linked account, with no server policy, so it is
                // treated like the other provider-credential pages.
                ["/query"] = AccessPolicy.SuperAdminLocal,
                // Account wizard: initial sync and account creation are SuperAdminLocal (AccountEndpoints).
                ["/accounts/add"] = AccessPolicy.SuperAdminLocal,
                // Account detail and schedule pages call /api/v1/accounts (SuperAdminLocal) and sync endpoints.
                ["/accounts/{Id:guid}"] = AccessPolicy.SuperAdminLocal,
                ["/accounts/{Id:guid}/schedule"] = AccessPolicy.SuperAdminLocal,
                // POST /api/v1/media/export is SuperAdminLocal with step-up (EvidenceEndpoints); the page exists to run it.
                ["/review/export"] = AccessPolicy.SuperAdminLocal,
                ["/cases/new"] = AccessPolicy.Review,
                ["/settings/security-events"] = AccessPolicy.ReadOnly,
                ["/settings/operators"] = AccessPolicy.SuperAdminLocal,
                ["/settings/operators/{Id:guid}"] = AccessPolicy.SuperAdminLocal,
                // Approving credentials needs SuperAdminLocalOrPasskey (DeviceManagementEndpoints approve route); the
                // list itself needs Admin, but the page exists only to act on approvals, so the stricter policy applies.
                ["/admin/pending-credentials"] = AccessPolicy.SuperAdminLocalOrPasskey,
                // In-process orchestrator calls (factory reset, key storage), not HTTP: no server policy backs this
                // page, so the table is the only gate. Local tier is the conservative choice.
                ["/settings/infrastructure"] = AccessPolicy.SuperAdminLocal,
                ["/tools/import-export"] = AccessPolicy.SuperAdminLocal,
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
            // A query-keyed row (e.g. an Analyze tab) wins over the bare path; otherwise the bare path is used.
            return Rules.TryGetValue(path, out policy) || Rules.TryGetValue(StripQuery(path), out policy);
        }

        /// <summary>True when the operator in <paramref name="ctx"/> may open <paramref name="path"/>. Unlisted paths are allowed.</summary>
        public static bool IsAllowed(string path, NavContext ctx)
        {
            return !TryGetPolicy(path, out AccessPolicy policy) || Allows(policy, ctx);
        }

        /// <summary>True when <paramref name="ctx"/> satisfies <paramref name="policy"/>. Mirrors the server's VideoForensicsPolicies.</summary>
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
