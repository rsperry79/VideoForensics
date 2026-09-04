namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// What an already-paired, already-identified Operator is allowed to do (plan §5.10). Assigned
    /// per Operator, not per device - a role travels with the person across every device they pair.
    /// Ordered so a numeric comparison (role >= Admin) works for "at least this level" checks.
    /// </summary>
    public enum OperatorRole
    {
        /// <summary>View everything - no mutations of any kind.</summary>
        ReadOnly = 0,

        /// <summary>Read-only, plus can create annotations (manual jamming incidents, flag/mark evidence). Cannot delete, export, or reconfigure.</summary>
        Review = 1,

        /// <summary>Review, plus evidence lifecycle actions (downloads, export, validate, legal hold place/release) and day-to-day configuration.</summary>
        Admin = 2,

        /// <summary>Admin, plus anything that changes the server's own trust/security posture or is otherwise irreversible (pairing, revocation, network tier, tunnel, key storage, media storage, accounts, factory reset). Additionally requires the Local network tier on every SuperAdmin-gated action.</summary>
        SuperAdmin = 3
    }

    /// <summary>
    /// Which network path a request arrived on (plan §5.2). Used both for the owner's own tier
    /// setting and for the physical-presence check on SuperAdmin actions (§5.10) - the latter checks
    /// the ACTUAL tier a specific request arrived via, regardless of which tiers are enabled overall.
    /// </summary>
    public enum NetworkTier
    {
        Local = 0,
        Network = 1,
        Internet = 2
    }
}
