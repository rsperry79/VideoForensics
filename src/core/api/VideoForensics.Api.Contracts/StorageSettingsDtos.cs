using VideoForensics.Client.Common.Contracts;
using VideoForensics.Providers.Common.Helpers.Platform;

namespace VideoForensics.Api.Contracts
{
    /// <summary>
    /// Data transfer object representing the current state of a storage category.
    /// </summary>
    /// <param name="Category">The storage category (Database, Media, etc.).</param>
    /// <param name="CurrentPath">The effective resolved absolute path of the storage location.</param>
    /// <param name="IsDefault">True if using the platform default; false if an override is configured.</param>
    /// <param name="UsedBytes">Total size of files under this storage folder in bytes.</param>
    /// <param name="FreeBytes">Free space available on the volume containing this folder in bytes.</param>
    /// <param name="IsRelocatable">False for Keys (unsafe to relocate live); true for all other categories.</param>
    /// <param name="RequiresRestart">True for Database and Logs (cannot be relocated while the app holds an open handle); false for others.</param>
    public record StorageCategoryStatusDto(
        StorageCategory Category,
        string CurrentPath,
        bool IsDefault,
        long UsedBytes,
        long FreeBytes,
        bool IsRelocatable,
        bool RequiresRestart
    );

    /// <summary>
    /// Data transfer object representing the full storage settings state.
    /// </summary>
    /// <param name="Categories">The status of all storage categories.</param>
    public record StorageSettingsDto(
        IReadOnlyList<StorageCategoryStatusDto> Categories
    );

    /// <summary>
    /// Data transfer object for a request to relocate a storage category to a new root path.
    /// </summary>
    /// <param name="Category">The storage category to relocate.</param>
    /// <param name="NewRootPath">The new root path where files for this category will be stored.</param>
    public record RelocateStorageCategoryRequestDto(
        StorageCategory Category,
        string NewRootPath
    );

    /// <summary>
    /// Data transfer object representing the result of a storage category relocation attempt.
    /// </summary>
    /// <param name="Succeeded">True if relocation succeeded; false otherwise.</param>
    /// <param name="ErrorMessage">Null on success; contains error description on failure.</param>
    /// <param name="RestartRequired">True if the app must be restarted for the relocation to take effect (Database or Logs categories).</param>
    public record RelocateStorageCategoryResultDto(
        bool Succeeded,
        string? ErrorMessage,
        bool RestartRequired
    );

    /// <summary>Extension methods for mapping storage settings domain types to/from DTOs.</summary>
    public static class StorageSettingsDtoMapping
    {
        /// <summary>
        /// Converts a domain StorageCategoryStatus to a StorageCategoryStatusDto.
        /// </summary>
        public static StorageCategoryStatusDto ToDto(this StorageCategoryStatus status)
        {
            return new StorageCategoryStatusDto(
                Category: status.Category,
                CurrentPath: status.CurrentPath,
                IsDefault: status.IsDefault,
                UsedBytes: status.UsedBytes,
                FreeBytes: status.FreeBytes,
                IsRelocatable: status.IsRelocatable,
                RequiresRestart: status.RequiresRestart
            );
        }

        /// <summary>
        /// Converts a StorageCategoryStatusDto to a domain StorageCategoryStatus.
        /// </summary>
        public static StorageCategoryStatus ToDomain(this StorageCategoryStatusDto dto)
        {
            return new StorageCategoryStatus(
                Category: dto.Category,
                CurrentPath: dto.CurrentPath,
                IsDefault: dto.IsDefault,
                UsedBytes: dto.UsedBytes,
                FreeBytes: dto.FreeBytes,
                IsRelocatable: dto.IsRelocatable,
                RequiresRestart: dto.RequiresRestart
            );
        }

        /// <summary>
        /// Converts a domain StorageSettings to a StorageSettingsDto.
        /// </summary>
        public static StorageSettingsDto ToDto(this StorageSettings settings)
        {
            return new StorageSettingsDto(
                Categories: settings.Categories.Select(c => c.ToDto()).ToList()
            );
        }

        /// <summary>
        /// Converts a StorageSettingsDto to a domain StorageSettings.
        /// </summary>
        public static StorageSettings ToDomain(this StorageSettingsDto dto)
        {
            return new StorageSettings(
                Categories: dto.Categories.Select(c => c.ToDomain()).ToList()
            );
        }

        /// <summary>
        /// Converts a domain RelocateStorageCategoryRequest to a RelocateStorageCategoryRequestDto.
        /// </summary>
        public static RelocateStorageCategoryRequestDto ToDto(this RelocateStorageCategoryRequest request)
        {
            return new RelocateStorageCategoryRequestDto(
                Category: request.Category,
                NewRootPath: request.NewRootPath
            );
        }

        /// <summary>
        /// Converts a RelocateStorageCategoryRequestDto to a domain RelocateStorageCategoryRequest.
        /// </summary>
        public static RelocateStorageCategoryRequest ToDomain(this RelocateStorageCategoryRequestDto dto)
        {
            return new RelocateStorageCategoryRequest(
                Category: dto.Category,
                NewRootPath: dto.NewRootPath
            );
        }

        /// <summary>
        /// Converts a domain RelocateStorageCategoryResult to a RelocateStorageCategoryResultDto.
        /// </summary>
        public static RelocateStorageCategoryResultDto ToDto(this RelocateStorageCategoryResult result)
        {
            return new RelocateStorageCategoryResultDto(
                Succeeded: result.Succeeded,
                ErrorMessage: result.ErrorMessage,
                RestartRequired: result.RestartRequired
            );
        }

        /// <summary>
        /// Converts a RelocateStorageCategoryResultDto to a domain RelocateStorageCategoryResult.
        /// </summary>
        public static RelocateStorageCategoryResult ToDomain(this RelocateStorageCategoryResultDto dto)
        {
            return new RelocateStorageCategoryResult(
                Succeeded: dto.Succeeded,
                ErrorMessage: dto.ErrorMessage,
                RestartRequired: dto.RestartRequired
            );
        }
    }
}
