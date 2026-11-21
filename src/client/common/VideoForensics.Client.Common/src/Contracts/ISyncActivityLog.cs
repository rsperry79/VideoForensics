namespace VideoForensics.Client.Common.Contracts
{
    /// <summary>
    /// Console lines for account syncs (start, discovery, events, config, validation, finish). Sync code appends lines.
    /// Two kinds of reader use them: the hub broadcaster drains pending lines once, and each open page reads the
    /// retained history by position, so every reader sees every line and a page opened mid-sync can show the output so far.
    /// </summary>
    public interface ISyncActivityLog
    {
        /// <summary>Adds one line. Safe to call from any thread.</summary>
        void Append(string line);

        /// <summary>Adds one failure line, logged at Error so failed syncs stand out in the server log. Safe to call from any thread.</summary>
        void AppendError(string line);

        /// <summary>Returns and removes every pending line, oldest first. Only the hub broadcaster calls this.</summary>
        IReadOnlyList<string> Drain();

        /// <summary>Total lines ever appended. Pass it back to <see cref="ReadSince"/> to read only newer lines.</summary>
        long Position { get; }

        /// <summary>
        /// Returns the retained lines at or after <paramref name="position"/>, oldest first, without removing them. A position
        /// older than the retained history starts at the oldest retained line. <paramref name="nextPosition"/> is the value to pass next time.
        /// </summary>
        IReadOnlyList<string> ReadSince(long position, out long nextPosition);
    }
}
