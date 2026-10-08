using System;

namespace VideoForensics.Core.Logging.Services
{
    /// <summary>
    /// Immutable log entry record captured in the in-memory buffer.
    /// Each entry has a monotonic sequence number for resume/pagination.
    /// </summary>
    public readonly record struct LogRecord(
        long Sequence,
        DateTimeOffset TimestampUtc,
        string Level,
        string Category,
        string Message,
        string? Exception);
}
