namespace VideoForensics.Client.Common.Contracts
{
    /// <summary>
    /// Console lines for account syncs (start, discovery, events, config, validation, finish). Sync code appends lines;
    /// the progress broadcaster drains them alongside the download service's own lines, so every sync type shows output.
    /// </summary>
    public interface ISyncActivityLog
    {
        /// <summary>Adds one line. Safe to call from any thread.</summary>
        void Append(string line);

        /// <summary>Returns and removes every pending line, oldest first.</summary>
        IReadOnlyList<string> Drain();
    }
}
