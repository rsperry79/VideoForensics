using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>
    /// Tests for RingHistoryEventCache implementation using IMemoryCache.
    /// Verifies cache widening logic, composite entry preservation, and concurrency safety.
    /// </summary>
    public class RingHistoryEventCacheTests
    {
        /// <summary>
        /// Test double for Session that returns predictable events.
        /// </summary>
        private class TestSession : Session
        {
            public TestSession(string username = "test@example.com") : base(username, "password") { }

            public override async Task<List<DoorbotHistoryEvent>> GetDoorbotsHistory(DateTime startDate, DateTime? endDate, long? doorbotId = null)
            {
                // Return events spread across the requested range
                var events = new List<DoorbotHistoryEvent>();
                var effectiveEnd = endDate ?? DateTime.MaxValue;

                if (effectiveEnd > startDate)
                {
                    var current = startDate;
                    int id = 1;
                    while (current < effectiveEnd && (effectiveEnd - current).TotalHours > 0)
                    {
                        events.Add(new DoorbotHistoryEvent
                        {
                            Id = id++,
                            CreatedAt = current.ToString("o"),
                            Kind = "motion",
                            SnapshotUrl = null
                        });
                        current = current.AddHours(1);
                    }
                }

                return await Task.FromResult(events);
            }
        }

        /// <summary>
        /// Test double whose fetches for registered (start, end) ranges signal "entered" and then
        /// block until released, so a test controls the order in which concurrent fetches finish.
        /// </summary>
        private sealed class GatedSession : TestSession
        {
            public sealed class Gate
            {
                public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
                public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            private readonly Dictionary<(DateTime Start, DateTime End), Gate> _gates = [];

            public Gate AddGate(DateTime start, DateTime end)
            {
                var gate = new Gate();
                _gates[(start, end)] = gate;
                return gate;
            }

            public override async Task<List<DoorbotHistoryEvent>> GetDoorbotsHistory(DateTime startDate, DateTime? endDate, long? doorbotId = null)
            {
                if (endDate.HasValue && _gates.TryGetValue((startDate, endDate.Value), out var gate))
                {
                    gate.Entered.TrySetResult();
                    await gate.Release.Task;
                }

                return await base.GetDoorbotsHistory(startDate, endDate, doorbotId);
            }
        }

        [Fact]
        public async Task RingHistoryEventCache_PreservesWideningMetadata_Under100ConcurrentRequests()
        {
            // Arrange
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var logger = new Mock<ILogger>().Object;
            var cache = new RingHistoryEventCache(memoryCache, logger);
            var session = new TestSession();
            var baseDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var cts = new CancellationTokenSource();

            // Act: Spawn 100 concurrent tasks requesting overlapping date ranges
            // Task 0-33: Request [baseDate, baseDate+30d]
            // Task 33-66: Request [baseDate-10d, baseDate+20d] (wider start)
            // Task 66-100: Request [baseDate-5d, baseDate+40d] (even wider)
            var tasks = new List<Task>();

            for (int i = 0; i < 100; i++)
            {
                int taskNum = i;
                var task = Task.Run(async () =>
                {
                    DateTime start, end;
                    if (taskNum < 33)
                    {
                        start = baseDate;
                        end = baseDate.AddDays(30);
                    }
                    else if (taskNum < 66)
                    {
                        start = baseDate.AddDays(-10);
                        end = baseDate.AddDays(20);
                    }
                    else
                    {
                        start = baseDate.AddDays(-5);
                        end = baseDate.AddDays(40);
                    }

                    var result = await cache.GetEventsAsync(session, start, end, cts.Token);

                    // Verify result is not null and contains events
                    Assert.NotNull(result);
                    Assert.True(result.Count > 0, $"Task {taskNum} received empty result for range [{start}, {end}]");
                });

                tasks.Add(task);
            }

            await Task.WhenAll(tasks);

            // Assert: Final cache state should have widened metadata preserved in composite entry
            // The widest request range should become the cache bounds
            // widest start: baseDate - 10 days (from tasks 33-66)
            // widest end: baseDate + 40 days (from tasks 66-100)
            var expectedStart = baseDate.AddDays(-10);
            var expectedEnd = baseDate.AddDays(40);

            // Verify the cache marked the range as cached (hint check should return true)
            // This indirectly verifies that metadata was preserved through concurrent requests
            // and that the composite entry stored both events and metadata
            Assert.True(
                cache.IsCached(session, expectedStart, expectedEnd),
                "Cache metadata was not preserved - IsCached returned false for the widened range");

            // Also verify a sub-range is cached (widening worked)
            Assert.True(
                cache.IsCached(session, baseDate, baseDate.AddDays(20)),
                "Sub-range not cached - widening metadata lost or corrupted");

            memoryCache.Dispose();
        }

        [Fact]
        public async Task GetEventsAsync_WithinCachedRange_ReturnsFilteredEventsWithoutFetch()
        {
            // Arrange
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var logger = new Mock<ILogger>().Object;
            var cache = new RingHistoryEventCache(memoryCache, logger);

            var session = new TestSession();
            var baseDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var cts = new CancellationTokenSource();

            // Act: First request to populate cache
            var firstResult = await cache.GetEventsAsync(
                session,
                baseDate,
                baseDate.AddDays(20),
                cts.Token);

            // Assert: First request should have made a call
            Assert.NotNull(firstResult);
            Assert.True(firstResult.Count > 0);

            // Act: Second request within cached range
            var secondResult = await cache.GetEventsAsync(
                session,
                baseDate.AddDays(8),
                baseDate.AddDays(12),
                cts.Token);

            // Assert: Second request should return results from cache
            Assert.NotNull(secondResult);
            Assert.True(secondResult.Count > 0);

            // Verify all events in the result fall within the requested range
            foreach (var evt in secondResult)
            {
                Assert.NotNull(evt.CreatedAtDateTime);
                Assert.True(evt.CreatedAtDateTime.Value >= baseDate.AddDays(8));
                Assert.True(evt.CreatedAtDateTime.Value <= baseDate.AddDays(12));
            }

            memoryCache.Dispose();
        }

        [Fact]
        public async Task GetEventsAsync_WidensCache_WhenRequestExtendsExistingRange()
        {
            // Arrange
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var logger = new Mock<ILogger>().Object;
            var cache = new RingHistoryEventCache(memoryCache, logger);

            var session = new TestSession();
            var baseDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var cts = new CancellationTokenSource();

            // Act: First request [0, 20]
            var firstResult = await cache.GetEventsAsync(
                session,
                baseDate,
                baseDate.AddDays(20),
                cts.Token);

            Assert.NotNull(firstResult);

            // Act: Second request that extends the range [0, 40]
            var secondResult = await cache.GetEventsAsync(
                session,
                baseDate,
                baseDate.AddDays(40),
                cts.Token);

            // Assert: The cache should now report the extended range as cached
            // This verifies the composite entry was updated with new boundaries
            Assert.True(cache.IsCached(session, baseDate, baseDate.AddDays(40)),
                "Extended range should be cached after widening request");

            // Also verify the original range is still cached
            Assert.True(cache.IsCached(session, baseDate, baseDate.AddDays(20)),
                "Original range should still be cached");

            memoryCache.Dispose();
        }

        [Fact]
        public void IsCached_WithoutPopulatedCache_ReturnsFalse()
        {
            // Arrange
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var logger = new Mock<ILogger>().Object;
            var cache = new RingHistoryEventCache(memoryCache, logger);
            var baseDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            // Act & Assert
            Assert.False(cache.IsCached(new TestSession(), baseDate, baseDate.AddDays(10)));

            memoryCache.Dispose();
        }

        [Fact]
        public async Task RingHistoryEventCache_SlowerWidening_DoesNotNarrowCachedRange()
        {
            // Arrange
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var logger = new Mock<ILogger>().Object;
            var cache = new RingHistoryEventCache(memoryCache, logger);
            var session = new GatedSession();
            var d = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var ct = timeout.Token;

            // Seed the cache with [D, D+5] before gating anything.
            await cache.GetEventsAsync(session, d, d.AddDays(5), ct);

            // X widens the start (fetch covers [D-10, D+5]) and is held in flight.
            var xGate = session.AddGate(d.AddDays(-10), d.AddDays(5));
            // Y widens the end; if it reads the stale seed it fetches [D, D+40].
            var yGate = session.AddGate(d, d.AddDays(40));

            // Act
            var x = cache.GetEventsAsync(session, d.AddDays(-10), d.AddDays(5), ct);
            await xGate.Entered.Task.WaitAsync(ct);

            var y = cache.GetEventsAsync(session, d, d.AddDays(40), ct);
            await Task.WhenAny(yGate.Entered.Task, Task.Delay(500));

            if (yGate.Entered.Task.IsCompleted)
            {
                // No lock: Y stored [D, D+40] first, then the slower X overwrites it with the narrower range.
                yGate.Release.SetResult();
                await y.WaitAsync(ct);
                xGate.Release.SetResult();
                await x.WaitAsync(ct);
            }
            else
            {
                // Lock present: Y waits for X, then re-reads the cache and widens from X's stored range.
                xGate.Release.SetResult();
                await x.WaitAsync(ct);
                await y.WaitAsync(ct);
            }

            var yResult = await y;

            // Assert
            Assert.True(
                cache.IsCached(session, d, d.AddDays(40)),
                "The widened end (D+40) was not preserved: a slower, narrower fetch overwrote the wider cached range");
            Assert.NotEmpty(yResult);

            memoryCache.Dispose();
        }
    }
}
