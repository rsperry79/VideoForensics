namespace VideoForensics.Client.Common.Contracts
{
    /// <summary>
    /// Exports a provider account's retained data to a JSON file in the exports folder on a background thread,
    /// optionally hard-deleting the account only after the export succeeds. Job status is held in memory only:
    /// a server restart loses it, while an export file already written on disk survives.
    /// </summary>
    public interface IAccountExportService
    {
        /// <summary>Queues an export and returns its job id immediately; the work runs on a background thread.</summary>
        /// <param name="accountId">The provider account to export.</param>
        /// <param name="purgeAfterExport">When true, hard-deletes the account after the export file is written.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <param name="stepUpToken">
        /// Step-up token the server requires for this action. Used only by the remote implementation, which sends it as
        /// the X-StepUp-Token header; the local implementation ignores it.
        /// </param>
        Task<Guid> QueueExportAsync(Guid accountId, bool purgeAfterExport, CancellationToken ct, string? stepUpToken = null);

        /// <summary>Gets the current status of a job, or null if unknown (including after a restart).</summary>
        /// <param name="jobId">The job id returned by <see cref="QueueExportAsync"/>.</param>
        /// <param name="ct">Cancellation token.</param>
        Task<ExportJobStatus?> GetStatusAsync(Guid jobId, CancellationToken ct);
    }
}
