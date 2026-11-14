namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>
    /// Forensic case reads and commands, with DTO signatures. Client hosts use this interface. The server implements it
    /// over the database (ICaseWriteRepository). Every operation here has a matching HTTP route on the server.
    /// </summary>
    public interface ICaseRepository
    {
        /// <summary>Creates a case and returns it.</summary>
        Task<ForensicCaseDto> CreateAsync(
            string? caseNumber,
            string title,
            string? description,
            Guid? leadOperatorId,
            DateTime? scopeFromUtc,
            DateTime? scopeToUtc,
            IReadOnlyCollection<Guid> deviceIds,
            string createdBy,
            CancellationToken ct);

        /// <summary>Gets a case by id, or null.</summary>
        Task<ForensicCaseDto?> GetAsync(Guid id, CancellationToken ct);

        /// <summary>Gets a case by its case number, or null.</summary>
        Task<ForensicCaseDto?> GetByNumberAsync(string caseNumber, CancellationToken ct);

        /// <summary>Lists cases, optionally filtered by status.</summary>
        Task<IReadOnlyList<ForensicCaseDto>> ListAsync(CaseStatusDto? status, CancellationToken ct);

        /// <summary>Lists cases for a provider account.</summary>
        Task<IReadOnlyList<ForensicCaseDto>> ListForProviderAccountAsync(Guid providerAccountId, CancellationToken ct);

        /// <summary>Updates a case's title, description and lead operator.</summary>
        Task UpdateDetailsAsync(
            Guid id,
            string title,
            string? description,
            Guid? leadOperatorId,
            string updatedBy,
            CancellationToken ct);

        /// <summary>Replaces a case's scope: its UTC range and its devices.</summary>
        Task SetScopeAsync(
            Guid id,
            DateTime? fromUtc,
            DateTime? toUtc,
            IReadOnlyCollection<Guid> deviceIds,
            string updatedBy,
            CancellationToken ct);

        /// <summary>The device ids in a case's scope.</summary>
        Task<IReadOnlyList<Guid>> GetDeviceIdsAsync(Guid caseId, CancellationToken ct);

        /// <summary>Closes a case.</summary>
        Task CloseAsync(Guid id, string closedBy, CancellationToken ct);

        /// <summary>Reopens a closed case.</summary>
        Task ReopenAsync(Guid id, string reopenedBy, CancellationToken ct);

        /// <summary>Adds an item to a case and returns it.</summary>
        Task<CaseItemDto> AddItemAsync(
            Guid caseId,
            CaseItemKindDto kind,
            Guid targetId,
            string reason,
            string addedBy,
            CancellationToken ct);

        /// <summary>Removes an item from a case, keeping the record.</summary>
        Task RemoveItemAsync(Guid caseItemId, string removedBy, string reason, CancellationToken ct);

        /// <summary>Lists a case's items, optionally including removed ones.</summary>
        Task<IReadOnlyList<CaseItemDto>> ListItemsAsync(Guid caseId, bool includeRemoved, CancellationToken ct);

        /// <summary>Lists cases that contain the given item.</summary>
        Task<IReadOnlyList<ForensicCaseDto>> ListCasesContainingAsync(CaseItemKindDto kind, Guid targetId, CancellationToken ct);

        /// <summary>Creates a case from a jamming incident.</summary>
        Task<ForensicCaseDto> CreateFromJammingDetectionAsync(JammingIncidentDto jammingIncident, CancellationToken ct);
    }
}
