using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>Extension methods for mapping ProviderSyncRun entities to sync run DTOs.</summary>
    public static class ProviderSyncRunDtoMapping
    {
        /// <summary>
        /// Converts a ProviderSyncRun entity to a ProviderSyncRunDetailDto.
        /// </summary>
        /// <param name="entity">The sync run entity to map.</param>
        /// <returns>The detail DTO, including the error message.</returns>
        public static ProviderSyncRunDetailDto ToDto(this VideoForensics.Data.Common.Entities.ProviderSyncRun entity)
        {
            return new ProviderSyncRunDetailDto(
                Id: entity.Id,
                ProviderAccountId: entity.ProviderAccountId,
                StartedUtc: entity.StartedUtc,
                CompletedUtc: entity.CompletedUtc,
                Success: entity.Success,
                ErrorMessage: entity.ErrorMessage);
        }

        /// <summary>
        /// Converts a ProviderSyncRun entity to a ProviderSyncRunSummaryDto for list views.
        /// Named ToSummaryDto because C# cannot overload ToDto on return type alone.
        /// </summary>
        /// <param name="entity">The sync run entity to map.</param>
        /// <returns>The summary DTO, without the account ID or error message.</returns>
        public static ProviderSyncRunSummaryDto ToSummaryDto(this VideoForensics.Data.Common.Entities.ProviderSyncRun entity)
        {
            return new ProviderSyncRunSummaryDto(
                Id: entity.Id,
                StartedUtc: entity.StartedUtc,
                CompletedUtc: entity.CompletedUtc,
                Success: entity.Success,
                SyncType: entity.SyncType,
                DevicesSynced: entity.DevicesSynced,
                LocationsSynced: entity.LocationsSynced,
                EventsSynced: entity.EventsSynced,
                ConfigAreasSynced: entity.ConfigAreasSynced,
                FeedsFailed: entity.FeedsFailed,
                SnapshotsSynced: entity.SnapshotsSynced);
        }
    }
}
