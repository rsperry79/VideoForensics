using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Logging.Services;

namespace VideoForensics.Core.Api.Contracts
{
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
