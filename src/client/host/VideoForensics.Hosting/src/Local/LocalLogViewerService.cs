using System.Runtime.CompilerServices;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Logging.Services;

namespace VideoForensics.Client.Hosting.Local
{
    /// <summary>
    /// In-process <see cref="ILogViewerService"/> that reads the host's own <see cref="InMemoryLogBuffer"/>, so MAUI
    /// can show its process logs without an HTTP call. Mirrors the server behaviour in
    /// VideoForensics.WebApp/Api/LogEndpoints.cs (level validation, default and maximum limits, truncation).
    /// </summary>
    public sealed class LocalLogViewerService : ILogViewerService
    {
        private const int DefaultLimit = 500;
        private const int MaxLimit = 2000;

        private static readonly string[] ValidLogLevels = { "Trace", "Debug", "Information", "Warning", "Error", "Critical" };

        private readonly InMemoryLogBuffer _buffer;

        /// <summary>Creates a service over the given in-process log buffer.</summary>
        /// <param name="buffer">The buffer this process appends its logs to.</param>
        public LocalLogViewerService(InMemoryLogBuffer buffer)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            _buffer = buffer;
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentException">
        /// <paramref name="query"/> has an invalid <see cref="LogQuery.MinLevel"/>.
        /// </exception>
        public Task<LogPage> GetPageAsync(LogQuery query, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(query);
            ValidateMinLevel(query.MinLevel);

            ct.ThrowIfCancellationRequested();

            int clampedLimit = ClampLimit(query.Limit);

            // Request one extra record so truncation is known without a second pass, as the server does.
            List<LogRecord> snapshot = _buffer.GetSnapshot(NullIfEmpty(query.MinLevel), NullIfEmpty(query.Search), query.AfterSequence, clampedLimit + 1);

            bool truncated = snapshot.Count > clampedLimit;

            // Without AfterSequence the buffer returns the newest limit+1 matches ascending, so the extra record is at
            // the front and must be skipped; keeping Take() here would return the oldest rather than the newest entries.
            // With AfterSequence the buffer returns the oldest matches after the cursor, so Take() is correct.
            IEnumerable<LogRecord> kept = truncated && query.AfterSequence is null
                ? snapshot.Skip(snapshot.Count - clampedLimit)
                : snapshot.Take(clampedLimit);
            var entries = kept.Select(ToEntry).ToList();

            return Task.FromResult(new LogPage(entries, _buffer.LatestSequence, truncated));
        }

        /// <summary>
        /// Filters the in-process buffer by time range. MAUI has no log files, so a range older than the buffer's
        /// oldest entry returns what the buffer still holds rather than reading disk.
        /// </summary>
        /// <exception cref="ArgumentException">The MinLevel is invalid or the range ends before it starts.</exception>
        public Task<LogPage> GetHistoryAsync(LogHistoryQuery query, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(query);
            ValidateMinLevel(query.MinLevel);
            if (query.ToUtc < query.FromUtc)
            {
                throw new ArgumentException("The range must not end before it starts.", nameof(query));
            }

            ct.ThrowIfCancellationRequested();

            int clampedLimit = ClampLimit(query.Limit);
            List<LogRecord> inRange = _buffer
                .GetSnapshot(NullIfEmpty(query.MinLevel), NullIfEmpty(query.Search), null, int.MaxValue)
                .Where(r => r.TimestampUtc >= query.FromUtc && r.TimestampUtc <= query.ToUtc)
                .ToList();

            bool truncated = inRange.Count > clampedLimit;
            var entries = inRange.Take(clampedLimit).Select(ToEntry).ToList();
            return Task.FromResult(new LogPage(entries, 0, truncated));
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentException">The MinLevel is invalid.</exception>
        public async IAsyncEnumerable<LogEntry> StreamAsync(
            LogQuery query,
            [EnumeratorCancellation] CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(query);
            ValidateMinLevel(query.MinLevel);

            // Backlog and live subscription are taken atomically by the buffer, so no entry is lost or duplicated.
            (IReadOnlyList<LogRecord> backlog, IAsyncEnumerable<LogRecord> live) = _buffer.SubscribeWithBacklog(
                NullIfEmpty(query.MinLevel),
                NullIfEmpty(query.Search),
                query.AfterSequence,
                ClampLimit(query.Limit),
                ct);

            foreach (LogRecord record in backlog)
            {
                yield return ToEntry(record);
            }

            await foreach (LogRecord record in live.WithCancellation(ct))
            {
                yield return ToEntry(record);
            }
        }

        private static void ValidateMinLevel(string? minLevel)
        {
            if (!string.IsNullOrEmpty(minLevel) && !ValidLogLevels.Contains(minLevel, StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"MinLevel must be one of: {string.Join(", ", ValidLogLevels)}", nameof(minLevel));
            }
        }

        private static int ClampLimit(int limit)
        {
            return limit <= 0 ? DefaultLimit : Math.Min(limit, MaxLimit);
        }

        private static string? NullIfEmpty(string? value)
        {
            return string.IsNullOrEmpty(value) ? null : value;
        }

        private static LogEntry ToEntry(LogRecord record)
        {
            return new(record.Sequence, record.TimestampUtc, record.Level, record.Category, record.Message, record.Exception);
        }
    }
}
