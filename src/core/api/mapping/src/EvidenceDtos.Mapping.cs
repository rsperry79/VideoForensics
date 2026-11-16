using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>Extension methods for mapping ReconciliationDiscrepancy entities to/from ReconciliationDiscrepancyDtos.</summary>
    public static class ReconciliationDiscrepancyDtoMapping
    {
        /// <summary>
        /// Converts a ReconciliationDiscrepancy entity to a ReconciliationDiscrepancyDto.
        /// </summary>
        public static ReconciliationDiscrepancyDto ToDto(this VideoForensics.Data.Common.Entities.ReconciliationDiscrepancy entity)
        {
            return new ReconciliationDiscrepancyDto(
                Type: Enum.Parse<DiscrepancyTypeDto>(entity.Type.ToString()),
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
                Type = Enum.Parse<VideoForensics.Data.Common.Entities.DiscrepancyType>(dto.Type.ToString()),
                ProviderEventId = dto.ProviderEventId,
                FieldName = dto.FieldName,
                StoredValue = dto.StoredValue,
                ProviderValue = dto.ProviderValue
            };
        }
    }
}
