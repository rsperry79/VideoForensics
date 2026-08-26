namespace VideoForensics.Data.Core.Contracts
{
    /// <summary>Service for purging media files based on retention policy.</summary>
    public interface IRetentionService
    {
        /// <summary>
        /// Purges media files older than RetentionDaysDefault, deleting the on-disk file
        /// and setting IsPurged/PurgedAtUtc/PurgeReason. Logs each purge via IActionLogger.
        /// </summary>
        Task<int> PurgeExpiredAsync(CancellationToken ct);
    }
}
