namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>
    /// Row for the account details page's sync history list.
    /// </summary>
    /// <param name="Id">Sync run ID.</param>
    /// <param name="StartedUtc">UTC time the sync started.</param>
    /// <param name="CompletedUtc">UTC time the sync finished; null when the run did not complete.</param>
    /// <param name="Success">True when the sync finished without an account error.</param>
    public record ProviderSyncRunSummaryDto(
        Guid Id,
        DateTime StartedUtc,
        DateTime? CompletedUtc,
        bool Success
    );

    /// <summary>
    /// Full detail of a single account sync run, including the failure message for unsuccessful runs.
    /// </summary>
    /// <param name="Id">Sync run ID.</param>
    /// <param name="ProviderAccountId">The provider account this sync ran for.</param>
    /// <param name="StartedUtc">UTC time the sync started.</param>
    /// <param name="CompletedUtc">UTC time the sync finished; null when the run did not complete.</param>
    /// <param name="Success">True when the sync finished without an account error.</param>
    /// <param name="ErrorMessage">Failure message for an unsuccessful run; null when Success is true.</param>
    public record ProviderSyncRunDetailDto(
        Guid Id,
        Guid ProviderAccountId,
        DateTime StartedUtc,
        DateTime? CompletedUtc,
        bool Success,
        string? ErrorMessage
    );

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
                Success: entity.Success);
        }
    }
}
