namespace VideoForensics.Client.Common.Contracts
{
    /// <summary>
    /// A single server log entry as shown to a UI client. Independent of the wire DTO
    /// (<c>VideoForensics.Core.Api.Contracts.LogEntryDto</c>) because <c>Api.Contracts</c> references this project,
    /// so the reverse reference would be a cycle; <see cref="ILogViewerService"/> implementations map at the HTTP boundary.
    /// </summary>
    public record LogEntry(
        long Sequence,
        DateTimeOffset TimestampUtc,
        string Level,
        string Category,
        string Message,
        string? Exception);

    /// <summary>Filter for log history and streaming. <see cref="Limit"/> of 0 or less means the server default.</summary>
    public record LogQuery(
        string? MinLevel,
        string? Search,
        long? AfterSequence,
        int Limit = 0);

    /// <summary>A snapshot of log entries; <see cref="Truncated"/> is true when more entries matched than were returned.</summary>
    public record LogPage(
        IReadOnlyList<LogEntry> Entries,
        long LatestSequence,
        bool Truncated);

    /// <summary>
    /// Filter for past entries read from the daily log files. Both bounds are UTC and inclusive.
    /// <see cref="Limit"/> of 0 or less means the server default.
    /// </summary>
    public record LogHistoryQuery(
        DateTimeOffset FromUtc,
        DateTimeOffset ToUtc,
        string? MinLevel,
        string? Search,
        int Limit = 0);

    /// <summary>
    /// Reads the server's in-memory log (SuperAdmin on the Local network tier only). Read-only, so no passkey
    /// step-up is required; only destructive or system changes are challenged.
    /// </summary>
    public interface ILogViewerService
    {
        /// <summary>Retrieves a snapshot of log entries matching <paramref name="query"/>.</summary>
        /// <param name="query">Filter; <see cref="LogQuery.AfterSequence"/> returns only newer entries.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <exception cref="HttpRequestException">
        /// <see cref="HttpRequestException.StatusCode"/> is Unauthorized or Forbidden when the session, role or network tier
        /// is rejected; other failures carry their status.
        /// </exception>
        Task<LogPage> GetPageAsync(LogQuery query, CancellationToken ct);

        /// <summary>
        /// Streams backlog then live entries until the server closes the stream or <paramref name="ct"/> is cancelled.
        /// To resume after a disconnect, call again with <see cref="LogQuery.AfterSequence"/> set to the last sequence seen.
        /// </summary>
        /// <param name="query">Filter and resume position.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <exception cref="HttpRequestException">Same status semantics as <see cref="GetPageAsync"/>.</exception>
        IAsyncEnumerable<LogEntry> StreamAsync(LogQuery query, CancellationToken ct);

        /// <summary>
        /// Reads past entries in a date range, oldest first, from the server's daily log files. Use this for any window
        /// that is not live; the in-memory buffer only holds the newest entries.
        /// </summary>
        /// <param name="query">Range, filters and limit.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <exception cref="HttpRequestException">Same status semantics as <see cref="GetPageAsync"/>.</exception>
        Task<LogPage> GetHistoryAsync(LogHistoryQuery query, CancellationToken ct);
    }
}
