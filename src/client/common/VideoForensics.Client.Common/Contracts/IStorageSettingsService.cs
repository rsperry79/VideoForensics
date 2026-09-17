using VideoForensics.Providers.Common.Helpers.Platform;

namespace VideoForensics.Client.Common.Contracts
{
    /// <summary>
    /// Domain type representing the current state of a storage category.
    /// </summary>
    public record StorageCategoryStatus(
        StorageCategory Category,
        string CurrentPath,
        bool IsDefault,
        long UsedBytes,
        long FreeBytes,
        bool IsRelocatable,
        bool RequiresRestart
    );

    /// <summary>
    /// Domain type representing the full storage settings state.
    /// </summary>
    public record StorageSettings(
        IReadOnlyList<StorageCategoryStatus> Categories
    );

    /// <summary>
    /// Domain type for a request to relocate a storage category to a new root path.
    /// </summary>
    public record RelocateStorageCategoryRequest(
        StorageCategory Category,
        string NewRootPath
    );

    /// <summary>
    /// Domain type representing the result of a storage category relocation attempt.
    /// </summary>
    public record RelocateStorageCategoryResult(
        bool Succeeded,
        string? ErrorMessage,
        bool RestartRequired
    );

    /// <summary>
    /// Manages storage settings and relocation for various system storage categories.
    /// This service provides read-only access to current storage state and relocation capabilities
    /// for superadmin-only operations.
    /// </summary>
    public interface IStorageSettingsService
    {
        /// <summary>
        /// Retrieves the current status of all storage categories.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to allow operation cancellation.</param>
        /// <returns>A snapshot of all storage categories and their current state.</returns>
        Task<StorageSettings> GetStatusAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Relocates a storage category to a new root path.
        /// The operation may require an app restart for Database and Logs categories.
        /// </summary>
        /// <param name="request">Details of the relocation request including category and new path.</param>
        /// <param name="cancellationToken">Cancellation token to allow operation cancellation.</param>
        /// <returns>Result of the relocation attempt with success/error and restart requirement information.</returns>
        Task<RelocateStorageCategoryResult> RelocateAsync(RelocateStorageCategoryRequest request, CancellationToken cancellationToken);
    }
}
