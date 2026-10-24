using Microsoft.Extensions.DependencyInjection;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Logging.Services;
using VideoForensics.Client.Hosting.Local;

namespace VideoForensics.Client.Hosting.Tests.Local
{
    public class LocalLogViewerServiceTests
    {
        private const string Token = "local-step-up";

        private static readonly LogQuery EmptyQuery = new(null, null, null);
        private static readonly DateTimeOffset Timestamp = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

        private static LogRecord Rec(string level, string category, string message, string? exception = null)
        {
            return new(0, Timestamp, level, category, message, exception);
        }

        private static void Append(InMemoryLogBuffer buffer, LogRecord record)
        {
            buffer.Append(ref record);
        }

        [Fact]
        public async Task GetPageAsync_FiltersByMinLevelAndSearch_ReturnsMatchingEntries()
        {
            var buffer = new InMemoryLogBuffer();
            Append(buffer, Rec("Debug", "Cat", "debug disk noise"));   // 1: below min level
            Append(buffer, Rec("Warning", "Cat", "disk full"));        // 2: match
            Append(buffer, Rec("Error", "Other", "Disk failed"));      // 3: match (case-insensitive)
            Append(buffer, Rec("Information", "Cat", "disk ok"));      // 4: below min level
            Append(buffer, Rec("Error", "Cat", "network down"));       // 5: no search match
            var service = new LocalLogViewerService(buffer);

            LogPage page = await service.GetPageAsync(new LogQuery("Warning", "disk", null), CancellationToken.None);

            Assert.Equal(new long[] { 2, 3 }, page.Entries.Select(e => e.Sequence));
            Assert.False(page.Truncated);
            Assert.Equal(5, page.LatestSequence);
        }

        [Fact]
        public async Task GetPageAsync_AfterSequence_ReturnsOnlyNewerEntries()
        {
            var buffer = new InMemoryLogBuffer();
            Append(buffer, Rec("Information", "Cat", "one"));
            Append(buffer, Rec("Information", "Cat", "two"));
            Append(buffer, Rec("Information", "Cat", "three"));
            var service = new LocalLogViewerService(buffer);

            LogPage page = await service.GetPageAsync(new LogQuery(null, null, 1), CancellationToken.None);

            Assert.Equal(new long[] { 2, 3 }, page.Entries.Select(e => e.Sequence));
        }

        [Fact]
        public async Task GetPageAsync_MoreMatchesThanLimit_SetsTruncatedAndCapsEntries()
        {
            var buffer = new InMemoryLogBuffer();
            for (int i = 0; i < 5; i++)
            {
                Append(buffer, Rec("Information", "Cat", $"msg {i}"));
            }

            var service = new LocalLogViewerService(buffer);

            LogPage page = await service.GetPageAsync(new LogQuery(null, null, null, Limit: 2), CancellationToken.None);

            Assert.True(page.Truncated);
            Assert.Equal(2, page.Entries.Count);
            Assert.Equal(5, page.LatestSequence);
        }

        [Fact]
        public async Task GetPageAsync_MoreMatchesThanLimit_KeepsNewestEntries()
        {
            var buffer = new InMemoryLogBuffer();
            for (int i = 1; i <= 5; i++)
            {
                Append(buffer, Rec("Information", "Cat", $"msg {i}"));
            }

            var service = new LocalLogViewerService(buffer);

            LogPage page = await service.GetPageAsync(new LogQuery(null, null, null, Limit: 2), CancellationToken.None);

            Assert.True(page.Truncated);
            Assert.Equal(new long[] { 4, 5 }, page.Entries.Select(e => e.Sequence));
        }

        [Fact]
        public async Task GetPageAsync_LimitZero_UsesDefaultOf500()
        {
            var buffer = new InMemoryLogBuffer(capacity: 1000);
            for (int i = 0; i < 501; i++)
            {
                Append(buffer, Rec("Information", "Cat", $"msg {i}"));
            }

            var service = new LocalLogViewerService(buffer);

            LogPage page = await service.GetPageAsync(new LogQuery(null, null, null, Limit: 0), CancellationToken.None);

            Assert.True(page.Truncated);
            Assert.Equal(500, page.Entries.Count);
        }

        [Fact]
        public async Task GetPageAsync_LimitAboveMaximum_CapsAt2000()
        {
            var buffer = new InMemoryLogBuffer(capacity: 2500);
            for (int i = 0; i < 2001; i++)
            {
                Append(buffer, Rec("Information", "Cat", $"msg {i}"));
            }

            var service = new LocalLogViewerService(buffer);

            LogPage page = await service.GetPageAsync(new LogQuery(null, null, null, Limit: 5000), CancellationToken.None);

            Assert.True(page.Truncated);
            Assert.Equal(2000, page.Entries.Count);
        }

        [Fact]
        public async Task GetPageAsync_InvalidMinLevel_ThrowsArgumentException()
        {
            var service = new LocalLogViewerService(new InMemoryLogBuffer());

            _ = await Assert.ThrowsAsync<ArgumentException>(() =>
                service.GetPageAsync(new LogQuery("Loud", null, null), CancellationToken.None));
        }

        [Fact]
        public async Task GetPageAsync_NullQuery_ThrowsArgumentNullException()
        {
            var service = new LocalLogViewerService(new InMemoryLogBuffer());

            _ = await Assert.ThrowsAsync<ArgumentNullException>(() => service.GetPageAsync(null!, CancellationToken.None));
        }

        [Fact]
        public async Task GetPageAsync_MapsEveryLogRecordField_ToLogEntry()
        {
            var buffer = new InMemoryLogBuffer();
            var when = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
            var record = new LogRecord(0, when, "Error", "Cat.Name", "boom", "stack text");
            Append(buffer, record);
            var service = new LocalLogViewerService(buffer);

            LogPage page = await service.GetPageAsync(EmptyQuery, CancellationToken.None);

            LogEntry entry = Assert.Single(page.Entries);
            Assert.Equal(new LogEntry(1, when, "Error", "Cat.Name", "boom", "stack text"), entry);
        }

        [Fact]
        public async Task StreamAsync_YieldsBacklogThenLiveEntries()
        {
            var buffer = new InMemoryLogBuffer();
            Append(buffer, Rec("Information", "Cat", "backlog"));
            var service = new LocalLogViewerService(buffer);
            using var cts = new CancellationTokenSource();

            var received = new List<LogEntry>();
            var backlogSeen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var consume = Task.Run(async () =>
            {
                await foreach (LogEntry entry in service.StreamAsync(EmptyQuery, cts.Token))
                {
                    received.Add(entry);
                    if (received.Count == 1)
                    {
                        _ = backlogSeen.TrySetResult();
                    }

                    if (received.Count == 2)
                    {
                        break;
                    }
                }
            });

            // The backlog and live subscription are registered atomically, so once the backlog entry arrives the
            // live subscriber is in place and an append made now must be delivered.
            await backlogSeen.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Append(buffer, Rec("Error", "Cat", "live"));
            await consume.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(new long[] { 1, 2 }, received.Select(e => e.Sequence));
            Assert.Equal("live", received[1].Message);
        }

        [Fact]
        public async Task StreamAsync_CancelledToken_CompletesWithoutThrowingUnexpectedly()
        {
            var service = new LocalLogViewerService(new InMemoryLogBuffer());
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Matches the Remote implementation: a cancelled token surfaces as OperationCanceledException, nothing else.
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await foreach (LogEntry _ in service.StreamAsync(EmptyQuery, cts.Token))
                { }
            });
        }

        [Fact]
        public async Task AddLocalLogViewerService_ResolvesLocalLogViewerServiceAsLastRegistration()
        {
            var services = new ServiceCollection();

            _ = services.AddLocalLogViewerService();
            using ServiceProvider provider = services.BuildServiceProvider();

            ILogViewerService resolved = provider.GetRequiredService<ILogViewerService>();
            _ = Assert.IsType<LocalLogViewerService>(resolved);
        }
    }
}
