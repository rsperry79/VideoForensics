namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>
    /// Database administration for the Database page: stats, compact, repair, and validate with the purge and chain repair
    /// that follow a validation. Engine-specific work is delegated to <see cref="IDatabaseEngine"/>. Lifecycle work such as
    /// releasing connections stays on <c>IDatabaseMaintenanceService</c>.
    /// </summary>
    public interface IDatabaseAdministrationService
    {
        /// <summary>Physical stats plus the record counts that matter for evidence.</summary>
        Task<DatabaseStatsResult> GetStatsAsync(CancellationToken ct);

        /// <summary>Rebuilds the file to reclaim free pages.</summary>
        Task CompactAsync(CancellationToken ct);

        /// <summary>Integrity check, then index rebuild. Returns the integrity verdict from the engine.</summary>
        Task<string> RepairAsync(CancellationToken ct);

        /// <summary>Lists media rows whose file is missing and checks the action log hash chain. Changes nothing.</summary>
        Task<DatabaseValidationResult> ValidateAsync(CancellationToken ct);

        /// <summary>
        /// Purges the given media rows, skipping any under an active legal hold, and logs each purge. Returns the number purged.
        /// </summary>
        Task<int> PurgeMissingMediaAsync(IReadOnlyCollection<Guid> mediaItemIds, CancellationToken ct);

        /// <summary>
        /// Bridges every hash gap in the action log with a logged <c>ChainRepaired</c> entry. Tampered entries (a hash that does
        /// not match its contents) are never bridged, and are reported as unrepairable.
        /// </summary>
        Task<ChainRepairResult> RepairChainAsync(CancellationToken ct);
    }

    /// <summary>Database stats: the engine's physical layout plus record counts.</summary>
    /// <param name="File">Physical layout from the engine.</param>
    /// <param name="MediaItemCount">Media rows, including purged ones.</param>
    /// <param name="PurgedMediaItemCount">Media rows already purged.</param>
    /// <param name="ActionLogEntryCount">Entries in the action log hash chain.</param>
    public sealed record DatabaseStatsResult(
        DatabaseFileStats File,
        int MediaItemCount,
        int PurgedMediaItemCount,
        int ActionLogEntryCount);

    /// <summary>A media row whose file is not on disk.</summary>
    /// <param name="MediaItemId">Row id.</param>
    /// <param name="FileName">File name as recorded.</param>
    /// <param name="ResolvedPath">Full path the row resolves to.</param>
    public sealed record MissingMediaRecord(Guid MediaItemId, string FileName, string ResolvedPath);

    /// <summary>Validation findings. Nothing is changed by validation itself.</summary>
    /// <param name="MissingMedia">Non-purged media rows with no file on disk.</param>
    /// <param name="Chain">Action log chain verification.</param>
    public sealed record DatabaseValidationResult(
        IReadOnlyList<MissingMediaRecord> MissingMedia,
        ChainIntegrityResult Chain)
    {
        /// <summary>True when there is nothing to purge and the chain verifies.</summary>
        public bool IsClean => MissingMedia.Count == 0 && Chain.IsValid;
    }

    /// <summary>Outcome of a chain repair.</summary>
    /// <param name="BridgesAdded">Gaps bridged by a new <c>ChainRepaired</c> entry.</param>
    /// <param name="Unrepairable">The break that could not be bridged, when one remains: an entry whose contents were altered.</param>
    public sealed record ChainRepairResult(int BridgesAdded, ChainIntegrityResult? Unrepairable);
}
