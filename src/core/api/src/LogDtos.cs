using System.Text.Json.Serialization;

using VideoForensics.Core.Logging.Services;

namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>
    /// A single log entry for the in-memory buffer.
    /// </summary>
    /// <remarks>TimestampUtc, Level, Category and Message are required on the wire; deserialization throws if any is missing.</remarks>
    /// <param name="Sequence">Monotonic sequence number assigned by the buffer.</param>
    /// <param name="TimestampUtc">When the log entry was created, in UTC.</param>
    /// <param name="Level">Log level (e.g., "Information", "Warning", "Error").</param>
    /// <param name="Category">Logger category/source name.</param>
    /// <param name="Message">The formatted log message.</param>
    /// <param name="Exception">Exception details (type, message, stack trace), if any; truncated to ~4000 chars.</param>
    public record LogEntryDto(
        long Sequence,
        [property: JsonRequired] DateTimeOffset TimestampUtc,
        [property: JsonRequired] string Level,
        [property: JsonRequired] string Category,
        [property: JsonRequired] string Message,
        string? Exception);

    /// <summary>
    /// Query parameters for fetching log entries from the in-memory buffer.
    /// </summary>
    /// <param name="MinLevel">Minimum log level to include (e.g., "Warning", "Error"); null = all levels.</param>
    /// <param name="Search">Text search term for message/category/exception; null = no search filter.</param>
    /// <param name="AfterSequence">Only return entries with sequence > this value; null = no afterSequence filter.</param>
    /// <param name="Limit">Maximum entries to return (0 or negative = default 500, capped at 2000).</param>
    public record LogQueryDto(
        string? MinLevel,
        string? Search,
        long? AfterSequence,
        int Limit = 0);

    /// <summary>
    /// Paginated response of log entries with metadata.
    /// </summary>
    /// <param name="Entries">The log entries matching the query.</param>
    /// <param name="LatestSequence">The highest sequence number currently in the buffer.</param>
    /// <param name="Truncated">True if more entries matched than were returned (limit exceeded).</param>
    public record LogPageDto(
        IReadOnlyList<LogEntryDto> Entries,
        long LatestSequence,
        bool Truncated);

    /// <summary>Extension methods for mapping log domain types to/from DTOs.</summary>
    public static class LogDtoMapping
    {
        /// <summary>
        /// Converts a domain LogRecord to a LogEntryDto.
        /// </summary>
        public static LogEntryDto ToDto(this LogRecord record)
        {
            // A null from the logger must not become a missing field on the wire (the four fields are JsonRequired).
            return new LogEntryDto(
                Sequence: record.Sequence,
                TimestampUtc: record.TimestampUtc,
                Level: record.Level ?? string.Empty,
                Category: record.Category ?? string.Empty,
                Message: record.Message ?? string.Empty,
                Exception: record.Exception);
        }
    }
}