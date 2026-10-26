using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace VideoForensics.Core.Logging.Services
{
    /// <summary>
    /// Reads past entries from the daily JSON audit files that Serilog writes (<c>audit-yyyyMMdd.json</c>). The in-memory
    /// buffer only keeps the newest few thousand entries, so history for a date range has to come from these files.
    /// </summary>
    /// <remarks>
    /// Files are named by local day, so the reader opens one extra day on each side of the range and then keeps only the
    /// entries whose timestamp falls inside it. Lines that do not parse are skipped rather than failing the whole read,
    /// since one bad line (for example an unescaped exception) should not hide the rest of the day.
    /// </remarks>
    public static class LogFileHistoryReader
    {
        private const string FilePrefix = "audit-";
        private const string FileSuffix = ".json";
        private const string DayFormat = "yyyyMMdd";

        /// <summary>
        /// Returns up to <paramref name="limit"/> entries in the range, oldest first. <c>Truncated</c> is true when more
        /// entries matched than were returned.
        /// </summary>
        public static async Task<(IReadOnlyList<LogRecord> Records, bool Truncated)> ReadAsync(
            string directory,
            DateTimeOffset fromUtc,
            DateTimeOffset toUtc,
            string? minLevel,
            string? search,
            int limit,
            CancellationToken ct)
        {
            ArgumentException.ThrowIfNullOrEmpty(directory);
            if (limit <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(limit), limit, "The limit must be positive.");
            }

            var records = new List<LogRecord>();
            if (!Directory.Exists(directory) || toUtc < fromUtc)
            {
                return (records, false);
            }

            DateOnly firstDay = DateOnly.FromDateTime(fromUtc.UtcDateTime).AddDays(-1);
            DateOnly lastDay = DateOnly.FromDateTime(toUtc.UtcDateTime).AddDays(1);
            long sequence = 0;

            foreach (string path in DayFiles(directory, firstDay, lastDay))
            {
                ct.ThrowIfCancellationRequested();
                using var reader = new StreamReader(path);
                string? line;
                while ((line = await reader.ReadLineAsync(ct)) is not null)
                {
                    if (!TryParse(line, ++sequence, out LogRecord record))
                    {
                        continue;
                    }

                    if (record.TimestampUtc < fromUtc || record.TimestampUtc > toUtc || !PassesFilters(record, minLevel, search))
                    {
                        continue;
                    }

                    if (records.Count == limit)
                    {
                        return (records, true);
                    }

                    records.Add(record);
                }
            }

            return (records, false);
        }

        private static IEnumerable<string> DayFiles(string directory, DateOnly firstDay, DateOnly lastDay)
        {
            return Directory.EnumerateFiles(directory, FilePrefix + "*" + FileSuffix)
                .Select(path => (Path: path, Day: TryParseDay(Path.GetFileName(path))))
                .Where(f => f.Day is { } day && day >= firstDay && day <= lastDay)
                .OrderBy(f => f.Day)
                .Select(f => f.Path);
        }

        private static DateOnly? TryParseDay(string fileName)
        {
            string stem = fileName[FilePrefix.Length..^FileSuffix.Length];
            return DateOnly.TryParseExact(stem, DayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly day)
                ? day
                : null;
        }

        private static bool TryParse(string line, long sequence, out LogRecord record)
        {
            record = default;
            try
            {
                using JsonDocument doc = JsonDocument.Parse(line);
                JsonElement root = doc.RootElement;
                if (!root.TryGetProperty("Timestamp", out JsonElement ts)
                    || !DateTimeOffset.TryParse(ts.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset timestamp)
                    || !root.TryGetProperty("Level", out JsonElement level))
                {
                    return false;
                }

                string message = root.TryGetProperty("Message", out JsonElement m) ? m.GetString() ?? string.Empty : string.Empty;
                string? exception = root.TryGetProperty("Exception", out JsonElement ex) ? NullIfEmpty(ex.GetString()) : null;
                string category = "Unknown";
                if (root.TryGetProperty("Properties", out JsonElement props) && props.TryGetProperty("SourceContext", out JsonElement sc))
                {
                    category = sc.GetString() ?? category;
                }

                record = new LogRecord(sequence, timestamp.ToUniversalTime(), level.GetString() ?? "Information", category, message, exception);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static bool PassesFilters(LogRecord record, string? minLevel, string? search)
        {
            if (!string.IsNullOrEmpty(minLevel) && Priority(record.Level) < Priority(minLevel))
            {
                return false;
            }

            return string.IsNullOrEmpty(search)
                || record.Message.Contains(search, StringComparison.OrdinalIgnoreCase)
                || record.Category.Contains(search, StringComparison.OrdinalIgnoreCase);
        }

        // Same ordering as InMemoryLogBuffer: unknown levels count as Information.
        private static int Priority(string level)
        {
            return level.ToLowerInvariant() switch
            {
                "trace" => 0,
                "debug" => 1,
                "information" => 2,
                "warning" => 3,
                "error" => 4,
                "critical" => 5,
                _ => 2
            };
        }

        private static string? NullIfEmpty(string? value)
        {
            return string.IsNullOrEmpty(value) ? null : value;
        }
    }
}
