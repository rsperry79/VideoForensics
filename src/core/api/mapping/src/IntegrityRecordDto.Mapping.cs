using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Core.Api.Contracts
{
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
