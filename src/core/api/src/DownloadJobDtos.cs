namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>
    /// Request to queue a download job for one provider account. The server runs it in the background.
    /// </summary>
    /// <param name="AccountId">The provider account the job downloads for.</param>
    /// <param name="Kind">"Videos" or "Snapshots" (case-insensitive).</param>
    /// <param name="StartDateUtc">Start of the range (inclusive, UTC).</param>
    /// <param name="EndDateUtc">End of the range (inclusive, UTC). Must not be before the start.</param>
    /// <param name="Force">When true, files already on disk are downloaded again.</param>
    /// <param name="OutputPath">Folder for the downloaded files. Must resolve under the default download location.</param>
    public record CreateDownloadJobRequestDto(
        Guid AccountId,
        string Kind,
        DateTime StartDateUtc,
        DateTime EndDateUtc,
        bool Force,
        string OutputPath);

    /// <summary>State and counters of one download job.</summary>
    /// <param name="Id">Job id.</param>
    /// <param name="ProviderAccountId">The account the job downloads for.</param>
    /// <param name="Kind">"Videos" or "Snapshots".</param>
    /// <param name="StartDateUtc">Start of the range (UTC).</param>
    /// <param name="EndDateUtc">End of the range (UTC).</param>
    /// <param name="Force">Whether existing files are downloaded again.</param>
    /// <param name="OutputPath">Folder the files are written to.</param>
    /// <param name="State">"Queued", "Running", "Completed", "Failed", or "Cancelled".</param>
    /// <param name="CreatedUtc">When the job was queued (UTC).</param>
    /// <param name="StartedUtc">When the runner claimed the job (UTC), or null while queued.</param>
    /// <param name="FinishedUtc">When the job reached a terminal state (UTC), or null.</param>
    /// <param name="FilesDownloaded">Files written during the run.</param>
    /// <param name="FilesSkipped">Files skipped because they already exist.</param>
    /// <param name="FilesFailed">Files or devices that failed during the run.</param>
    /// <param name="ErrorMessage">Operator-facing failure reason, or null.</param>
    public record DownloadJobDto(
        Guid Id,
        Guid ProviderAccountId,
        string Kind,
        DateTime StartDateUtc,
        DateTime EndDateUtc,
        bool Force,
        string? OutputPath,
        string State,
        DateTime CreatedUtc,
        DateTime? StartedUtc,
        DateTime? FinishedUtc,
        int FilesDownloaded,
        int FilesSkipped,
        int FilesFailed,
        string? ErrorMessage);
}
