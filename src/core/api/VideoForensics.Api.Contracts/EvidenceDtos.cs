namespace VideoForensics.Api.Contracts
{
    /// <summary>
    /// Result of verifying a single media item's local file integrity.
    /// </summary>
    /// <param name="MediaItemId">Unique identifier for the media item.</param>
    /// <param name="FileName">Original file name.</param>
    /// <param name="Status">Verification status: 'verified', 'failed', or 'missing'.</param>
    /// <param name="FailureReason">Reason for verification failure, if Status is 'failed'.</param>
    public record MediaVerificationResultDto(
        Guid MediaItemId,
        string FileName,
        string Status,
        string? FailureReason
    );

    /// <summary>
    /// A discrepancy found during provider reconciliation.
    /// </summary>
    /// <param name="Type">Type of discrepancy found.</param>
    /// <param name="ProviderEventId">Provider's event identifier.</param>
    /// <param name="FieldName">Name of the field that changed, if applicable.</param>
    /// <param name="StoredValue">Value stored locally.</param>
    /// <param name="ProviderValue">Current value from the provider.</param>
    public record ReconciliationDiscrepancyDto(
        VideoForensics.Data.Common.Entities.DiscrepancyType Type,
        string ProviderEventId,
        string? FieldName,
        string? StoredValue,
        string? ProviderValue
    );

    /// <summary>Extension methods for mapping ReconciliationDiscrepancy entities to/from ReconciliationDiscrepancyDtos.</summary>
    public static class ReconciliationDiscrepancyDtoMapping
    {
        /// <summary>
        /// Converts a ReconciliationDiscrepancy entity to a ReconciliationDiscrepancyDto.
        /// </summary>
        public static ReconciliationDiscrepancyDto ToDto(this VideoForensics.Data.Common.Entities.ReconciliationDiscrepancy entity)
        {
            return new ReconciliationDiscrepancyDto(
                Type: entity.Type,
                ProviderEventId: entity.ProviderEventId,
                FieldName: entity.FieldName,
                StoredValue: entity.StoredValue,
                ProviderValue: entity.ProviderValue
            );
        }

        /// <summary>
        /// Converts a ReconciliationDiscrepancyDto to a ReconciliationDiscrepancy entity.
        /// </summary>
        public static VideoForensics.Data.Common.Entities.ReconciliationDiscrepancy ToDomain(this ReconciliationDiscrepancyDto dto)
        {
            return new VideoForensics.Data.Common.Entities.ReconciliationDiscrepancy
            {
                Type = dto.Type,
                ProviderEventId = dto.ProviderEventId,
                FieldName = dto.FieldName,
                StoredValue = dto.StoredValue,
                ProviderValue = dto.ProviderValue
            };
        }
    }

    /// <summary>
    /// Result of exporting evidence media items to an archive.
    /// </summary>
    /// <param name="Success">True if export completed successfully or partially with some items excluded.</param>
    /// <param name="ArchivePath">Full path to the created archive file, if successful.</param>
    /// <param name="ArchiveSha256Hash">SHA-256 hash of the archive file, if successful.</param>
    /// <param name="ItemsIncluded">Number of media items included in the archive.</param>
    /// <param name="ItemsExcludedForFailedIntegrity">Media item IDs excluded due to failed integrity verification.</param>
    /// <param name="ErrorMessage">Error message if the export failed entirely.</param>
    public record ExportResultDto(
        bool Success,
        string? ArchivePath,
        string? ArchiveSha256Hash,
        int ItemsIncluded,
        IReadOnlyList<Guid> ItemsExcludedForFailedIntegrity,
        string? ErrorMessage
    );
}
