namespace VideoForensics.Client.Common.Contracts
{
    /// <summary>
    /// A single server log entry as shown to a UI client. Independent of the wire DTO
    /// (<c>VideoForensics.Api.Contracts.LogEntryDto</c>) because <c>Api.Contracts</c> references this project,
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
    /// Reads the server's in-memory log (SuperAdmin on the Local network tier only). Both endpoints require a
    /// fresh step-up token, which the UI obtains via <c>WebAuthn.StepUpAsync</c> and passes per call.
    /// </summary>
    public interface ILogViewerService
    {
        /// <summary>Retrieves a snapshot of log entries matching <paramref name="query"/>.</summary>
        /// <param name="query">Filter; <see cref="LogQuery.AfterSequence"/> returns only newer entries.</param>
        /// <param name="stepUpToken">Step-up token sent as <c>X-StepUp-Token</c> on this request only.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <exception cref="ArgumentException"><paramref name="stepUpToken"/> is null or empty.</exception>
        /// <exception cref="HttpRequestException">
        /// <see cref="HttpRequestException.StatusCode"/> is Unauthorized or Forbidden when the session, role, network tier
        /// or step-up token is rejected (the UI should prompt for step-up again); other failures carry their status.
        /// </exception>
        Task<LogPage> GetPageAsync(LogQuery query, string stepUpToken, CancellationToken ct);

        /// <summary>
        /// Streams backlog then live entries until the server closes the stream or <paramref name="ct"/> is cancelled.
        /// To resume after a disconnect, call again with <see cref="LogQuery.AfterSequence"/> set to the last sequence
        /// seen; the token is only checked at connect time, so a reconnect needs a valid token again.
        /// </summary>
        /// <param name="query">Filter and resume position.</param>
        /// <param name="stepUpToken">Step-up token sent as <c>X-StepUp-Token</c> on the connect request only.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <exception cref="ArgumentException"><paramref name="stepUpToken"/> is null or empty.</exception>
        /// <exception cref="HttpRequestException">Same status semantics as <see cref="GetPageAsync"/>.</exception>
        IAsyncEnumerable<LogEntry> StreamAsync(LogQuery query, string stepUpToken, CancellationToken ct);
    }
}
