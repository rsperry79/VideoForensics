using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using VideoForensics.Core.Logging.Services;

using Xunit;

namespace VideoForensics.Core.Logging.Tests.Services
{
    public class LogFileHistoryReaderTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "vf-log-history-" + Guid.NewGuid().ToString("N"));

        public LogFileHistoryReaderTests()
        {
            _ = Directory.CreateDirectory(_directory);
        }

        public void Dispose()
        {
            Directory.Delete(_directory, recursive: true);
        }

        [Fact]
        public async Task ReadAsync_RangeAndLevel_ReturnsOnlyMatchingEntries()
        {
            WriteDay("20261026", Line("2026-10-26T10:00:00.000+00:00", "Information", "early"), Line("2026-10-26T11:00:00.000+00:00", "Trace", "quiet"), Line("2026-10-26T12:00:00.000+00:00", "Error", "late"));

            (IReadOnlyList<LogRecord> records, bool truncated) = await LogFileHistoryReader.ReadAsync(
                _directory,
                new DateTimeOffset(2026, 10, 26, 10, 30, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 10, 26, 12, 30, 0, TimeSpan.Zero),
                minLevel: "Information",
                search: null,
                limit: 100,
                CancellationToken.None);

            Assert.False(truncated);
            LogRecord only = Assert.Single(records);
            Assert.Equal("late", only.Message);
            Assert.Equal("Error", only.Level);
        }

        [Fact]
        public async Task ReadAsync_RangeSpansTwoDayFiles_ReadsBoth()
        {
            WriteDay("20261025", Line("2026-10-25T23:30:00.000+00:00", "Information", "previous day"));
            WriteDay("20261026", Line("2026-10-26T00:30:00.000+00:00", "Information", "next day"));

            (IReadOnlyList<LogRecord> records, _) = await LogFileHistoryReader.ReadAsync(
                _directory,
                new DateTimeOffset(2026, 10, 25, 23, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 10, 26, 1, 0, 0, TimeSpan.Zero),
                minLevel: null,
                search: null,
                limit: 100,
                CancellationToken.None);

            Assert.Equal(new[] { "previous day", "next day" }, records.Select(r => r.Message));
        }

        [Fact]
        public async Task ReadAsync_MoreMatchesThanLimit_ReturnsOldestUpToLimitAndReportsTruncated()
        {
            WriteDay("20261026",
                Line("2026-10-26T10:00:00.000+00:00", "Information", "one"),
                Line("2026-10-26T10:01:00.000+00:00", "Information", "two"),
                Line("2026-10-26T10:02:00.000+00:00", "Information", "three"));

            (IReadOnlyList<LogRecord> records, bool truncated) = await LogFileHistoryReader.ReadAsync(
                _directory,
                new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 10, 27, 0, 0, 0, TimeSpan.Zero),
                minLevel: null,
                search: null,
                limit: 2,
                CancellationToken.None);

            Assert.True(truncated);
            Assert.Equal(new[] { "one", "two" }, records.Select(r => r.Message));
        }

        [Fact]
        public async Task ReadAsync_MalformedLine_IsSkipped()
        {
            WriteDay("20261026", "this is not json", Line("2026-10-26T10:00:00.000+00:00", "Warning", "kept"));

            (IReadOnlyList<LogRecord> records, _) = await LogFileHistoryReader.ReadAsync(
                _directory,
                new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 10, 27, 0, 0, 0, TimeSpan.Zero),
                minLevel: null,
                search: null,
                limit: 100,
                CancellationToken.None);

            Assert.Equal("kept", Assert.Single(records).Message);
        }

        [Fact]
        public async Task ReadAsync_SourceContextProperty_BecomesCategory()
        {
            WriteDay("20261026", Line("2026-10-26T10:00:00.000+00:00", "Information", "hi", sourceContext: "VideoForensics.Client.Hosting.Thing"));

            (IReadOnlyList<LogRecord> records, _) = await LogFileHistoryReader.ReadAsync(
                _directory,
                new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 10, 27, 0, 0, 0, TimeSpan.Zero),
                minLevel: null,
                search: "thing",
                limit: 100,
                CancellationToken.None);

            LogRecord record = Assert.Single(records);
            Assert.Equal("VideoForensics.Client.Hosting.Thing", record.Category);
        }

        [Fact]
        public async Task ReadAsync_DayRolledBySize_ReadsEverySegmentOfTheDay()
        {
            // Serilog rolls a day's file on its size limit to audit-yyyyMMdd_001.json, _002, and so on.
            File.WriteAllLines(Path.Combine(_directory, "audit-20261026.json"), [Line("2026-10-26T10:00:00.000+00:00", "Information", "first")]);
            File.WriteAllLines(Path.Combine(_directory, "audit-20261026_001.json"), [Line("2026-10-26T11:00:00.000+00:00", "Information", "second")]);

            (IReadOnlyList<LogRecord> records, bool truncated) = await LogFileHistoryReader.ReadAsync(
                _directory,
                new DateTimeOffset(2026, 10, 26, 9, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero),
                minLevel: "Information",
                search: null,
                limit: 100,
                CancellationToken.None);

            Assert.False(truncated);
            Assert.Equal(["first", "second"], records.Select(r => r.Message));
        }

        private void WriteDay(string yyyymmdd, params string[] lines)
        {
            File.WriteAllLines(Path.Combine(_directory, $"audit-{yyyymmdd}.json"), lines);
        }

        private static string Line(string timestamp, string level, string message, string sourceContext = "Test.Category")
        {
            return "{\"Timestamp\":\"" + timestamp + "\",\"Level\":\"" + level + "\",\"Message\":\"" + message
                + "\",\"Exception\":\"\",\"Properties\":{\"SourceContext\":\"" + sourceContext + "\"}}";
        }
    }
}
