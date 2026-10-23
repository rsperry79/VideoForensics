using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>One cache instance may serve several Ring accounts; entries must stay separate per account.</summary>
    public class RingHistoryEventCacheAccountKeyTests
    {
        private static readonly DateTime Day = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>Counts how often the account is actually fetched from Ring.</summary>
        private sealed class CountingSession : Session
        {
            public CountingSession(string username) : base(username, "password") { }

            public int Fetches { get; private set; }

            public override async Task<List<DoorbotHistoryEvent>> GetDoorbotsHistory(DateTime startDate, DateTime? endDate, long? doorbotId = null)
            {
                Fetches++;
                return await Task.FromResult(new List<DoorbotHistoryEvent>
                {
                    new() { Id = Fetches, CreatedAt = startDate.ToString("o"), Kind = "motion" },
                });
            }
        }

        private static RingHistoryEventCache CreateCache()
        {
            return new(new MemoryCache(new MemoryCacheOptions()), NullLogger.Instance);
        }

        [Fact]
        public async Task GetEventsAsync_SecondAccount_IsNotServedFirstAccountsEntry()
        {
            RingHistoryEventCache cache = CreateCache();
            var first = new CountingSession("first@example.com");
            var second = new CountingSession("second@example.com");

            _ = await cache.GetEventsAsync(first, Day, Day.AddDays(5), CancellationToken.None);
            _ = await cache.GetEventsAsync(second, Day, Day.AddDays(5), CancellationToken.None);

            Assert.Equal(1, first.Fetches);
            Assert.Equal(1, second.Fetches);
        }

        [Fact]
        public async Task IsCached_OtherAccountHasCachedTheRange_ReturnsFalse()
        {
            RingHistoryEventCache cache = CreateCache();
            var first = new CountingSession("first@example.com");
            var second = new CountingSession("second@example.com");
            _ = await cache.GetEventsAsync(first, Day, Day.AddDays(5), CancellationToken.None);

            Assert.True(cache.IsCached(first, Day, Day.AddDays(5)));
            Assert.False(cache.IsCached(second, Day, Day.AddDays(5)));
        }

        [Fact]
        public async Task GetEventsAsync_SessionWithoutAccountIdentity_FetchesEveryTimeAndIsNotCached()
        {
            RingHistoryEventCache cache = CreateCache();
            var anonymous = new CountingSession(string.Empty);

            _ = await cache.GetEventsAsync(anonymous, Day, Day.AddDays(5), CancellationToken.None);
            _ = await cache.GetEventsAsync(anonymous, Day, Day.AddDays(5), CancellationToken.None);

            Assert.Equal(2, anonymous.Fetches);
            Assert.False(cache.IsCached(anonymous, Day, Day.AddDays(5)));
        }
    }
}
