namespace VideoForensics.Api.Contracts
{
    /// <summary>
    /// Data transfer object for an append-only record of media file integrity verification.
    /// </summary>
    /// <param name="Id">Unique identifier for this integrity verification record.</param>
    /// <param name="MediaItemId">The media item whose integrity was verified.</param>
    /// <param name="Sha256Hash">The SHA-256 hash value that was verified during this check.</param>
    /// <param name="VerifiedAtUtc">Timestamp of when the verification was performed, in UTC.</param>
    /// <param name="Passed">True if the media file hash matched the expected value; False if a hash mismatch was detected.</param>
    /// <param name="FailureReason">Reason for a failed verification (e.g., "hash_mismatch", "file_not_found"), if Passed is False. Null if verification passed.</param>
    /// <param name="VerifiedBy">Identifier of the operator, service, or system that performed this verification (e.g., username, service name).</param>
    public record IntegrityRecordDto(
        Guid Id,
        Guid MediaItemId,
        string Sha256Hash,
        DateTime VerifiedAtUtc,
        bool Passed,
        string? FailureReason,
        string VerifiedBy
    );

    /// <summary>Extension methods for mapping IntegrityRecord entities to/from IntegrityRecordDtos.</summary>
    public static class IntegrityRecordDtoMapping
    {
        /// <summary>
        /// Converts an IntegrityRecord entity to an IntegrityRecordDto.
        /// </summary>
        public static IntegrityRecordDto ToDto(this VideoForensics.Data.Common.Entities.IntegrityRecord entity)
        {
            return new IntegrityRecordDto(
                Id: entity.Id,
                MediaItemId: entity.MediaItemId,
                Sha256Hash: entity.Sha256Hash,
                VerifiedAtUtc: entity.VerifiedAtUtc,
                Passed: entity.Passed,
                FailureReason: entity.FailureReason,
                VerifiedBy: entity.VerifiedBy
            );
        }

        /// <summary>
        /// Converts an IntegrityRecordDto to an IntegrityRecord entity.
        /// </summary>
        public static VideoForensics.Data.Common.Entities.IntegrityRecord ToDomain(this IntegrityRecordDto dto)
        {
            return new VideoForensics.Data.Common.Entities.IntegrityRecord
            {
                Id = dto.Id,
                MediaItemId = dto.MediaItemId,
                Sha256Hash = dto.Sha256Hash,
                VerifiedAtUtc = dto.VerifiedAtUtc,
                Passed = dto.Passed,
                FailureReason = dto.FailureReason,
                VerifiedBy = dto.VerifiedBy
            };
        }
    }
}
