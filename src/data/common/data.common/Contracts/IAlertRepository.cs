using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for system-generated alerts (e.g., jamming detections).</summary>
    public interface IAlertRepository
    {
        /// <summary>
        /// Creates a new alert and persists it to the database.
        /// </summary>
        /// <param name="alert">The alert entity to create (Id will be generated if default).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The created Alert entity.</returns>
        Task<Alert> CreateAsync(Alert alert, CancellationToken ct);

        /// <summary>
        /// Retrieves an alert by its ID, or null if not found.
        /// </summary>
        Task<Alert?> GetAsync(Guid id, CancellationToken ct);

        /// <summary>
        /// Retrieves an alert by its related case ID, or null if not found.
        /// </summary>
        Task<Alert?> GetByCaseIdAsync(Guid caseId, CancellationToken ct);

        /// <summary>
        /// Lists all alerts, optionally filtered by status. Returns newest first.
        /// </summary>
        Task<IReadOnlyList<Alert>> ListAsync(string? status, CancellationToken ct);
    }
}
