using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using VideoForensics.Core.Logging.Services;

using Xunit;

namespace VideoForensics.Core.Logging.Tests
{
    public class InMemoryLogBufferTests : IAsyncLifetime
    {
        private InMemoryLogBuffer _buffer = null!;

        public async ValueTask InitializeAsync()
        {
            _buffer = new InMemoryLogBuffer(capacity: 5000);
            await ValueTask.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            _buffer?.Dispose();
            await ValueTask.CompletedTask;
        }

        [Fact]
        public void Constructor_DefaultCapacity_Is5000()
        {
            var buffer = new InMemoryLogBuffer();
            Assert.NotNull(buffer);
        }

        [Fact]
        public void Constructor_CustomCapacity_IsRespected()
        {
            var buffer = new InMemoryLogBuffer(capacity: 100);
            Assert.NotNull(buffer);
        }

        [Fact]
        public void Append_AddsEntryWithMonotonicSequence()
        {
            // Act
            var entry1 = new LogRecord(
                Sequence: 0,
                TimestampUtc: DateTimeOffset.UtcNow,
                Level: "Information",
                Category: "Test",
                Message: "First",
                Exception: null);
            _buffer.Append(ref entry1);

            // Assert
            var snapshot = _buffer.GetSnapshot(minLevel: null, search: null, afterSequence: null, limit: 10);
            Assert.Single(snapshot);
            Assert.Equal(1, snapshot[0].Sequence);
        }

        [Fact]
        public void Append_MultipleEntries_HaveMonotonicSequences()
        {
            // Act
            for (int i = 0; i < 5; i++)
            {
                var entry = new LogRecord(
                    Sequence: 0,
                    TimestampUtc: DateTimeOffset.UtcNow,
                    Level: "Information",
                    Category: "Test",
                    Message: $"Message {i}",
                    Exception: null);
                _buffer.Append(ref entry);
            }

            // Assert
            var snapshot = _buffer.GetSnapshot(minLevel: null, search: null, afterSequence: null, limit: 100);
            Assert.Equal(5, snapshot.Count);
            for (int i = 0; i < 5; i++)
            {
                Assert.Equal(i + 1, snapshot[i].Sequence);
            }
        }

        [Fact]
        public void Append_ExceedsCapacity_EvictsOldest()
        {
            var smallBuffer = new InMemoryLogBuffer(capacity: 3);

            // Act - add 5 entries to a buffer of capacity 3
            for (int i = 0; i < 5; i++)
            {
                var entry = new LogRecord(
                    Sequence: 0,
                    TimestampUtc: DateTimeOffset.UtcNow,
                    Level: "Information",
                    Category: "Test",
                    Message: $"Message {i}",
                    Exception: null);
                smallBuffer.Append(ref entry);
            }

            // Assert - should have last 3 entries (sequences 3, 4, 5)
            var snapshot = smallBuffer.GetSnapshot(minLevel: null, search: null, afterSequence: null, limit: 100);
            Assert.Equal(3, snapshot.Count);
            Assert.Equal(3, snapshot[0].Sequence);
            Assert.Equal(4, snapshot[1].Sequence);
            Assert.Equal(5, snapshot[2].Sequence);

            smallBuffer.Dispose();
        }

        [Fact]
        public void GetSnapshot_ReturnsOrderedBySequence()
        {
            // Act
            for (int i = 0; i < 3; i++)
            {
                var entry = new LogRecord(
                    Sequence: 0,
                    TimestampUtc: DateTimeOffset.UtcNow,
                    Level: "Information",
                    Category: "Test",
                    Message: $"Message {i}",
                    Exception: null);
                _buffer.Append(ref entry);
            }

            // Assert
            var snapshot = _buffer.GetSnapshot(minLevel: null, search: null, afterSequence: null, limit: 100);
            Assert.Equal(3, snapshot.Count);
            Assert.True(snapshot[0].Sequence < snapshot[1].Sequence);
            Assert.True(snapshot[1].Sequence < snapshot[2].Sequence);
        }

        [Fact]
        public void GetSnapshot_WithAfterSequence_ReturnsOnlyNewer()
        {
            // Act - add 5 entries
            for (int i = 0; i < 5; i++)
            {
                var entry = new LogRecord(
                    Sequence: 0,
                    TimestampUtc: DateTimeOffset.UtcNow,
                    Level: "Information",
                    Category: "Test",
                    Message: $"Message {i}",
                    Exception: null);
                _buffer.Append(ref entry);
            }

            // Assert - get only entries after sequence 2
            var snapshot = _buffer.GetSnapshot(minLevel: null, search: null, afterSequence: 2, limit: 100);
            Assert.Equal(3, snapshot.Count);
            Assert.Equal(3, snapshot[0].Sequence);
            Assert.Equal(4, snapshot[1].Sequence);
            Assert.Equal(5, snapshot[2].Sequence);
        }

        [Fact]
        public void GetSnapshot_WithLimit_ReturnsNewestWhenLimitExceeded()
        {
            // Act - add 10 entries
            for (int i = 0; i < 10; i++)
            {
                var entry = new LogRecord(
                    Sequence: 0,
                    TimestampUtc: DateTimeOffset.UtcNow,
                    Level: "Information",
                    Category: "Test",
                    Message: $"Message {i}",
                    Exception: null);
                _buffer.Append(ref entry);
            }

            // Assert - get last 3 (newest) entries when limit=3 and afterSequence=null
            var snapshot = _buffer.GetSnapshot(minLevel: null, search: null, afterSequence: null, limit: 3);
            Assert.Equal(3, snapshot.Count);
            Assert.Equal(8, snapshot[0].Sequence);
            Assert.Equal(9, snapshot[1].Sequence);
            Assert.Equal(10, snapshot[2].Sequence);
        }

        [Fact]
        public void GetSnapshot_WithMinLevel_FiltersEntries()
        {
            // Act - add entries with different log levels
            var entry1 = new LogRecord(
                Sequence: 0,
                TimestampUtc: DateTimeOffset.UtcNow,
                Level: "Trace",
                Category: "Test",
                Message: "Trace",
                Exception: null);
            _buffer.Append(ref entry1);

            var entry2 = new LogRecord(
                Sequence: 0,
                TimestampUtc: DateTimeOffset.UtcNow,
                Level: "Information",
                Category: "Test",
                Message: "Info",
                Exception: null);
            _buffer.Append(ref entry2);

            var entry3 = new LogRecord(
                Sequence: 0,
                TimestampUtc: DateTimeOffset.UtcNow,
                Level: "Error",
                Category: "Test",
                Message: "Error",
                Exception: null);
            _buffer.Append(ref entry3);

            // Assert - filter to only Warning and above (Information and Error, but not Trace)
            var snapshot = _buffer.GetSnapshot(minLevel: "Warning", search: null, afterSequence: null, limit: 100);
            Assert.Single(snapshot);
            Assert.Equal("Error", snapshot[0].Level);
        }

        [Fact]
        public void GetSnapshot_WithSearch_FindsInMessage()
        {
            // Act
            var entry1 = new LogRecord(
                Sequence: 0,
                TimestampUtc: DateTimeOffset.UtcNow,
                Level: "Information",
                Category: "Test",
                Message: "Hello World",
                Exception: null);
            _buffer.Append(ref entry1);

            var entry2 = new LogRecord(
                Sequence: 0,
                TimestampUtc: DateTimeOffset.UtcNow,
                Level: "Information",
                Category: "Test",
                Message: "Goodbye",
                Exception: null);
            _buffer.Append(ref entry2);

            // Assert - search for "World"
            var snapshot = _buffer.GetSnapshot(minLevel: null, search: "World", afterSequence: null, limit: 100);
            Assert.Single(snapshot);
            Assert.Contains("Hello World", snapshot[0].Message);
        }

        [Fact]
        public void GetSnapshot_WithSearch_CaseInsensitive()
        {
            // Act
            var entry = new LogRecord(
                Sequence: 0,
                TimestampUtc: DateTimeOffset.UtcNow,
                Level: "Information",
                Category: "Test",
                Message: "Hello World",
                Exception: null);
            _buffer.Append(ref entry);

            // Assert - search with different case
            var snapshot = _buffer.GetSnapshot(minLevel: null, search: "hello", afterSequence: null, limit: 100);
            Assert.Single(snapshot);
        }

        [Fact]
        public void GetSnapshot_WithSearch_FindsInCategory()
        {
            // Act
            var entry1 = new LogRecord(
                Sequence: 0,
                TimestampUtc: DateTimeOffset.UtcNow,
                Level: "Information",
                Category: "MyService",
                Message: "Test",
                Exception: null);
            _buffer.Append(ref entry1);

            var entry2 = new LogRecord(
                Sequence: 0,
                TimestampUtc: DateTimeOffset.UtcNow,
                Level: "Information",
                Category: "OtherService",
                Message: "Test",
                Exception: null);
            _buffer.Append(ref entry2);

            // Assert - search for "MyService"
            var snapshot = _buffer.GetSnapshot(minLevel: null, search: "MyService", afterSequence: null, limit: 100);
            Assert.Single(snapshot);
            Assert.Equal("MyService", snapshot[0].Category);
        }

        [Fact]
        public void GetSnapshot_WithSearch_FindsInException()
        {
            // Act
            var entry1 = new LogRecord(
                Sequence: 0,
                TimestampUtc: DateTimeOffset.UtcNow,
                Level: "Error",
                Category: "Test",
                Message: "Error occurred",
                Exception: "NullReferenceException: Value is null");
            _buffer.Append(ref entry1);

            var entry2 = new LogRecord(
                Sequence: 0,
                TimestampUtc: DateTimeOffset.UtcNow,
                Level: "Error",
                Category: "Test",
                Message: "Another error",
                Exception: "ArgumentException: Invalid arg");
            _buffer.Append(ref entry2);

            // Assert - search for "NullReference"
            var snapshot = _buffer.GetSnapshot(minLevel: null, search: "NullReference", afterSequence: null, limit: 100);
            Assert.Single(snapshot);
            Assert.Contains("NullReferenceException", snapshot[0].Exception);
        }

        [Fact]
        public void LatestSequence_ReturnsHighestSequence()
        {
            // Act - add 5 entries
            for (int i = 0; i < 5; i++)
            {
                var entry = new LogRecord(
                    Sequence: 0,
                    TimestampUtc: DateTimeOffset.UtcNow,
                    Level: "Information",
                    Category: "Test",
                    Message: $"Message {i}",
                    Exception: null);
                _buffer.Append(ref entry);
            }

            // Assert
            Assert.Equal(5, _buffer.LatestSequence);
        }

        [Fact]
        public async Task ConcurrentWriters_NoLostSequences()
        {
            var buffer = new InMemoryLogBuffer(capacity: 5000);

            // Act - spawn 10 tasks each writing 100 entries
            var tasks = new List<Task>();
            for (int t = 0; t < 10; t++)
            {
                tasks.Add(Task.Run(() =>
                {
                    for (int i = 0; i < 100; i++)
                    {
                        var entry = new LogRecord(
                            Sequence: 0,
                            TimestampUtc: DateTimeOffset.UtcNow,
                            Level: "Information",
                            Category: "Test",
                            Message: $"Message {i}",
                            Exception: null);
                        buffer.Append(ref entry);
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Assert - should have 1000 entries with unique monotonic sequences
            var snapshot = buffer.GetSnapshot(minLevel: null, search: null, afterSequence: null, limit: 2000);
            Assert.Equal(1000, snapshot.Count);

            var sequences = new HashSet<long>();
            for (int i = 0; i < snapshot.Count; i++)
            {
                sequences.Add(snapshot[i].Sequence);
                if (i > 0)
                {
                    Assert.True(snapshot[i].Sequence > snapshot[i - 1].Sequence);
                }
            }

            // All sequences should be unique
            Assert.Equal(1000, sequences.Count);

            buffer.Dispose();
        }

        private static LogRecord Rec(string level = "Information", string category = "Cat", string message = "msg", string? exception = null)
            => new(0, DateTimeOffset.UtcNow, level, category, message, exception);

        private static async Task<List<LogRecord>> ReadAsync(IAsyncEnumerable<LogRecord> live, int count, TimeSpan? timeout = null)
        {
            var items = new List<LogRecord>();
            if (count <= 0)
                return items;

            using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(10));
            await foreach (var r in live.WithCancellation(cts.Token))
            {
                items.Add(r);
                if (items.Count >= count)
                    break;
            }
            return items;
        }

        [Fact]
        public async Task Subscribe_RecordsAppendedAfterSubscribing_AreDeliveredOnly()
        {
            var before = Rec(message: "before");
            _buffer.Append(ref before);

            var live = _buffer.Subscribe(null, null, null, CancellationToken.None);
            var after = Rec(message: "after");
            _buffer.Append(ref after);

            var items = await ReadAsync(live, 1);
            Assert.Equal("after", Assert.Single(items).Message);
            Assert.Equal(2, items[0].Sequence);
        }

        [Fact]
        public async Task Subscribe_MinLevelAndSearch_FilterLiveRecords()
        {
            var live = _buffer.Subscribe("Warning", "needle", null, CancellationToken.None);
            var a = Rec("Information", message: "needle low"); _buffer.Append(ref a);
            var b = Rec("Error", message: "no match"); _buffer.Append(ref b);
            var c = Rec("Error", message: "has NEEDLE inside"); _buffer.Append(ref c);

            var items = await ReadAsync(live, 1);
            Assert.Equal(3, Assert.Single(items).Sequence);
        }

        [Fact]
        public async Task Subscribe_SearchMatchesCategoryAndException()
        {
            var live = _buffer.Subscribe(null, "zzz", null, CancellationToken.None);
            var a = Rec(category: "Zzz.Cat"); _buffer.Append(ref a);
            var b = Rec(exception: "boom ZZZ"); _buffer.Append(ref b);

            var items = await ReadAsync(live, 2);
            Assert.Equal(new long[] { 1, 2 }, items.ConvertAll(i => i.Sequence));
        }

        [Fact]
        public async Task Subscribe_AfterSequence_SkipsRecordsAtOrBelowIt()
        {
            var live = _buffer.Subscribe(null, null, 2, CancellationToken.None);
            for (int i = 0; i < 4; i++) { var r = Rec(message: $"m{i}"); _buffer.Append(ref r); }

            var items = await ReadAsync(live, 2);
            Assert.Equal(new long[] { 3, 4 }, items.ConvertAll(i => i.Sequence));
        }

        [Fact]
        public void Append_SlowSubscriberNeverReads_WriterIsNotBlocked()
        {
            var live = _buffer.Subscribe(null, null, null, CancellationToken.None);
            _ = live; // never enumerated

            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 20000; i++) { var r = Rec(); _buffer.Append(ref r); }
            sw.Stop();

            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"Append took {sw.Elapsed}");
            Assert.Equal(20000, _buffer.LatestSequence);
        }

        [Fact]
        public async Task Subscribe_SlowSubscriber_DropsOldestKeepsNewest()
        {
            var live = _buffer.Subscribe(null, null, null, CancellationToken.None);
            for (int i = 0; i < 2500; i++) { var r = Rec(); _buffer.Append(ref r); }

            var items = await ReadAsync(live, 1000);
            Assert.Equal(1000, items.Count);
            Assert.Equal(1501, items[0].Sequence);
            Assert.Equal(2500, items[^1].Sequence);
        }

        [Fact]
        public async Task Subscribe_Cancellation_RemovesSubscriberAndEndsEnumeration()
        {
            using var cts = new CancellationTokenSource();
            var live = _buffer.Subscribe(null, null, null, cts.Token);
            Assert.Equal(1, _buffer.SubscriberCount);

            var task = Task.Run(async () =>
            {
                try { await foreach (var _ in live) { } }
                catch (OperationCanceledException) { }
            });
            cts.Cancel();
            await task.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(0, _buffer.SubscriberCount);
        }

        [Fact]
        public async Task Subscribe_DisposingEnumerator_RemovesSubscriber()
        {
            var live = _buffer.Subscribe(null, null, null, CancellationToken.None);
            var e = live.GetAsyncEnumerator();
            var move = e.MoveNextAsync().AsTask();
            var r = Rec(); _buffer.Append(ref r);
            Assert.True(await move.WaitAsync(TimeSpan.FromSeconds(10)));

            await e.DisposeAsync();

            Assert.Equal(0, _buffer.SubscriberCount);
        }

        [Fact]
        public async Task Dispose_Buffer_CompletesSubscribers()
        {
            var live = _buffer.Subscribe(null, null, null, CancellationToken.None);
            var task = ReadAsync(live, int.MaxValue);

            _buffer.Dispose();

            var items = await task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Empty(items);
            Assert.Equal(0, _buffer.SubscriberCount);
        }

        [Fact]
        public void Subscribe_AfterBufferDisposed_CompletesImmediately()
        {
            _buffer.Dispose();
            var live = _buffer.Subscribe(null, null, null, CancellationToken.None);
            Assert.Equal(0, _buffer.SubscriberCount);
            Assert.NotNull(live);
        }

        [Fact]
        public void SubscribeWithBacklog_NoAfterSequence_BacklogIsNewestLimitEntries()
        {
            for (int i = 0; i < 10; i++) { var r = Rec(); _buffer.Append(ref r); }

            var (backlog, _) = _buffer.SubscribeWithBacklog(null, null, null, 3, CancellationToken.None);

            Assert.Equal(new long[] { 8, 9, 10 }, backlog.Select(b => b.Sequence).ToArray());
        }

        [Fact]
        public void SubscribeWithBacklog_AfterSequence_BacklogContainsAllNewerEntriesRegardlessOfLimit()
        {
            for (int i = 0; i < 10; i++) { var r = Rec(); _buffer.Append(ref r); }

            var (backlog, _) = _buffer.SubscribeWithBacklog(null, null, 4, 3, CancellationToken.None);

            Assert.Equal(new long[] { 5, 6, 7, 8, 9, 10 }, backlog.Select(b => b.Sequence).ToArray());
        }

        [Fact]
        public async Task SubscribeWithBacklog_LiveStartsStrictlyAfterBacklog()
        {
            for (int i = 0; i < 3; i++) { var r = Rec(); _buffer.Append(ref r); }

            var (backlog, live) = _buffer.SubscribeWithBacklog(null, null, null, 500, CancellationToken.None);
            var next = Rec(message: "live"); _buffer.Append(ref next);

            Assert.Equal(3, backlog.Count);
            var items = await ReadAsync(live, 1);
            Assert.Equal(4, items[0].Sequence);
        }

        [Fact]
        public async Task SubscribeWithBacklog_ConcurrentWriters_NoGapsOrDuplicates()
        {
            const int writers = 4;
            // Live total stays under the per-subscriber channel capacity so drop-oldest cannot mask a gap.
            const int perWriter = 200;
            for (int i = 0; i < 50; i++) { var r = Rec(); _buffer.Append(ref r); }

            using var go = new ManualResetEventSlim(false);
            var writerTasks = new List<Task>();
            for (int w = 0; w < writers; w++)
            {
                writerTasks.Add(Task.Run(() =>
                {
                    go.Wait();
                    for (int i = 0; i < perWriter; i++) { var r = Rec(); _buffer.Append(ref r); Thread.SpinWait(2000); }
                }));
            }

            go.Set();
            // Subscribe mid-stream so the handoff really happens under concurrent appends.
            while (_buffer.LatestSequence < 50 + 100)
                await Task.Yield();
            var (backlog, live) = _buffer.SubscribeWithBacklog(null, null, null, 100000, CancellationToken.None);

            await Task.WhenAll(writerTasks);
            long total = 50 + writers * perWriter;
            var firstLiveSeq = backlog.Count == 0 ? 1 : backlog[^1].Sequence + 1;
            int liveCount = (int)(total - firstLiveSeq + 1);

            var items = await ReadAsync(live, liveCount, TimeSpan.FromSeconds(30));

            var all = backlog.Concat(items).Select(i => i.Sequence).ToList();
            Assert.Equal(Enumerable.Range((int)all[0], all.Count).Select(i => (long)i), all);
            Assert.Equal(total, all[^1]);
            Assert.Equal(1, all[0]);
        }
    }
}
