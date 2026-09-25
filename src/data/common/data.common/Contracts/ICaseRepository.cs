using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for forensic cases with chain-of-custody logging for all mutations.</summary>
    public interface ICaseRepository
    {
        /// <summary>
        /// Creates a new forensic case with optional scope and device set.
        /// Appends a chain-of-custody ActionLog entry ("CreateCase") in the same operation.
        /// Throws InvalidOperationException if CaseNumber already exists.
        /// </summary>
        Task<ForensicCase> CreateAsync(
            string caseNumber,
            string title,
            string? description,
            Guid? leadOperatorId,
            DateTime? scopeFromUtc,
            DateTime? scopeToUtc,
            IReadOnlyCollection<Guid> deviceIds,
            string createdBy,
            CancellationToken ct);

        /// <summary>Retrieves a case by its ID, or null if not found.</summary>
        Task<ForensicCase?> GetAsync(Guid id, CancellationToken ct);

        /// <summary>Retrieves a case by its case number, or null if not found.</summary>
        Task<ForensicCase?> GetByNumberAsync(string caseNumber, CancellationToken ct);

        /// <summary>Lists cases, optionally filtered by status. Returns newest first. </summary>
        Task<IReadOnlyList<ForensicCase>> ListAsync(CaseStatus? status, CancellationToken ct);

        /// <summary>
        /// Updates a case's title, description, and lead operator.
        /// Appends a chain-of-custody ActionLog entry ("UpdateCase") in the same operation.
        /// Updates the case's UpdatedAtUtc timestamp.
        /// Throws InvalidOperationException if the case is Closed.
        /// </summary>
        Task UpdateDetailsAsync(
            Guid id,
            string title,
            string? description,
            Guid? leadOperatorId,
            string updatedBy,
            CancellationToken ct);

        /// <summary>
        /// Updates a case's scope (time window and device set).
        /// Replaces the device set entirely; appends a chain-of-custody ActionLog entry ("SetCaseScope") with details listing device counts and time window.
        /// Updates the case's UpdatedAtUtc timestamp.
        /// Throws InvalidOperationException if the case is Closed.
        /// </summary>
        Task SetScopeAsync(
            Guid id,
            DateTime? fromUtc,
            DateTime? toUtc,
            IReadOnlyCollection<Guid> deviceIds,
            string updatedBy,
            CancellationToken ct);

        /// <summary>Retrieves the IDs of all devices in a case's scope.</summary>
        Task<IReadOnlyList<Guid>> GetDeviceIdsAsync(Guid caseId, CancellationToken ct);

        /// <summary>
        /// Closes a case and appends a chain-of-custody ActionLog entry ("CloseCase").
        /// Throws InvalidOperationException if the case is already Closed.
        /// </summary>
        Task CloseAsync(Guid id, string closedBy, CancellationToken ct);

        /// <summary>
        /// Reopens a closed case and appends a chain-of-custody ActionLog entry ("ReopenCase").
        /// </summary>
        Task ReopenAsync(Guid id, string reopenedBy, CancellationToken ct);

        /// <summary>
        /// Pins an event or media item to a case.
        /// If Kind is Media, snapshots the MediaItem.Sha256Hash at add time into MediaSha256AtAdd.
        /// Appends a chain-of-custody ActionLog entry ("AddCaseItem") with entity type "Case".
        /// Throws InvalidOperationException if the item is already actively pinned to this case, if the target doesn't exist, or if the case is Closed.
        /// </summary>
        Task<CaseItem> AddItemAsync(
            Guid caseId,
            CaseItemKind kind,
            Guid targetId,
            string reason,
            string addedBy,
            CancellationToken ct);

        /// <summary>
        /// Soft-removes an item from a case (never hard-deleted for audit trail).
        /// Appends a chain-of-custody ActionLog entry ("RemoveCaseItem").
        /// </summary>
        Task RemoveItemAsync(Guid caseItemId, string removedBy, string reason, CancellationToken ct);

        /// <summary>
        /// Lists evidence items pinned to a case.
        /// If includeRemoved is false (default), excludes soft-removed items.
        /// If includeRemoved is true, includes soft-removed items (RemovedAtUtc != null).
        /// </summary>
        Task<IReadOnlyList<CaseItem>> ListItemsAsync(Guid caseId, bool includeRemoved, CancellationToken ct);

        /// <summary>
        /// Lists all cases that have an active pin (not soft-removed) of the specified kind and target ID.
        /// Returns only cases with RemovedAtUtc == null items matching the criteria.
        /// </summary>
        Task<IReadOnlyList<ForensicCase>> ListCasesContainingAsync(CaseItemKind kind, Guid targetId, CancellationToken ct);
    }
}
