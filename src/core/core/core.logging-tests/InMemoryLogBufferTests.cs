using System;
using System.Collections.Generic;
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
    }
}
