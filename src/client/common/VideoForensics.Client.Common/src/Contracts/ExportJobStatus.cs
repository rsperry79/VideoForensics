namespace VideoForensics.Client.Common.Contracts
{
    /// <summary>Snapshot of a background account export job.</summary>
    /// <param name="JobId">The job id returned when the export was queued (not the account id).</param>
    /// <param name="AccountId">The provider account being exported.</param>
    /// <param name="State">Current lifecycle state.</param>
    /// <param name="FilePath">Final export file path once written; null before that.</param>
    /// <param name="ErrorMessage">Failure reason when <paramref name="State"/> is Failed.</param>
    /// <param name="PurgeAfterExport">Whether the account is hard-deleted after a successful export.</param>
    /// <param name="CreatedUtc">When the job was queued.</param>
    /// <param name="CompletedUtc">When the job reached Succeeded or Failed.</param>
    public sealed record ExportJobStatus(
        Guid JobId,
        Guid AccountId,
        ExportJobState State,
        string? FilePath,
        string? ErrorMessage,
        bool PurgeAfterExport,
        DateTime CreatedUtc,
        DateTime? CompletedUtc);
}
