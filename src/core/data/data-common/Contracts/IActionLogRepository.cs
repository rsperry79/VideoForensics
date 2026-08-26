using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for hash-chained action log entries.</summary>
    public interface IActionLogRepository
    {
        /// <summary>Gets an action log entry by ID.</summary>
        Task<ActionLogEntry?> GetAsync(Guid entryId, CancellationToken ct);

        /// <summary>
        /// Appends a new entry to the hash chain. The repository - not the caller - reads the last
        /// entry's hash and computes this entry's PreviousEntryHash/EntryHash internally.
        /// Must be atomic: read the last entry hash, compute the new hash, and insert the row as a
        /// single transaction inside the repository implementation. Concurrent calls outside a shared
        /// transaction could fork the chain, so callers (e.g. IActionLogger) must never compute or pass
        /// a hash themselves - only the caller-controlled fields below.
        /// </summary>
        Task<ActionLogEntry> AppendAsync(
            string actor,
            ActorType actorType,
            string action,
            string entityType,
            Guid? entityId,
            string? detailsJson,
            CancellationToken ct);

        /// <summary>Gets the history of actions for a specific entity.</summary>
        Task<IReadOnlyList<ActionLogEntry>> GetHistoryForEntityAsync(string entityType, Guid entityId, CancellationToken ct);

        /// <summary>Gets all action log entries.</summary>
        Task<IReadOnlyList<ActionLogEntry>> ListAsync(CancellationToken ct);

        /// <summary>Verifies the integrity of the hash chain, returning true if the chain is valid.</summary>
        Task<bool> VerifyChainIntegrityAsync(CancellationToken ct);
    }
}
