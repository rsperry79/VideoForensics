namespace VideoForensics.Hosting.Contracts
{
    /// <summary>
    /// Runs an account's first sync in place of a normal sync while its <c>InitialSyncCompleted</c> flag is false. The
    /// server implements this, because the first sync's media step (validate-all) lives on the server. Hosts without it
    /// keep the normal sync path.
    /// </summary>
    public interface IInitialSyncDispatcher
    {
        /// <summary>Runs the account's first sync. Failures are recorded on the account, so the next sync retries it.</summary>
        Task RunInitialSyncAsync(Guid accountId, CancellationToken ct);
    }
}
