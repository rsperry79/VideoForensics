using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

using VideoForensics.Providers.Ring.Exceptions;
using VideoForensics.Providers.Ring.Services;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>Ring 429 handling: wait Retry-After (or 60s), retry the same operation, rethrow hard bans. Runs without sleeping.</summary>
    public class RingThrottleRetryTests
    {
        [Fact]
        public async Task ThrottleRetry_ThrottledWithRetryAfter_WaitsRetryAfterThenRetriesEvent()
        {
            var waits = new List<TimeSpan>();
            int calls = 0;

            bool ok = await ThrottleRetry.RunAsync(
                () =>
                {
                    calls++;
                    if (calls == 1)
                    {
                        throw new ThrottledException("HTTP 429") { RetryAfter = TimeSpan.FromSeconds(7) };
                    }

                    return Task.CompletedTask;
                },
                (wait, _) => { waits.Add(wait); return Task.CompletedTask; },
                CancellationToken.None);

            Assert.True(ok);
            Assert.Equal(2, calls);
            Assert.Equal(new[] { TimeSpan.FromSeconds(7) }, waits);
        }

        [Fact]
        public async Task ThrottleRetry_ThrottledWithoutRetryAfter_Waits60SecondsThenRetries()
        {
            var waits = new List<TimeSpan>();
            int calls = 0;

            bool ok = await ThrottleRetry.RunAsync(
                () =>
                {
                    calls++;
                    if (calls == 1)
                    {
                        throw new ThrottledException("HTTP 429");
                    }

                    return Task.CompletedTask;
                },
                (wait, _) => { waits.Add(wait); return Task.CompletedTask; },
                CancellationToken.None);

            Assert.True(ok);
            Assert.Equal(new[] { TimeSpan.FromSeconds(60) }, waits);
        }

        [Fact]
        public async Task ThrottleRetry_StillThrottledAfterMaxAttempts_ReturnsFalseWithoutAbortingCaller()
        {
            int waits = 0;

            bool ok = await ThrottleRetry.RunAsync(
                () => throw new ThrottledException("HTTP 429"),
                (_, _) => { waits++; return Task.CompletedTask; },
                CancellationToken.None,
                maxAttempts: 2);

            Assert.False(ok);
            Assert.Equal(1, waits);
        }

        [Fact]
        public async Task ThrottleRetry_HardBan_RethrowsWithoutWaiting()
        {
            int waits = 0;

            await Assert.ThrowsAsync<ThrottledException>(() => ThrottleRetry.RunAsync(
                () => throw new ThrottledException("hard ban", isHardBan: true),
                (_, _) => { waits++; return Task.CompletedTask; },
                CancellationToken.None));

            Assert.Equal(0, waits);
        }
    }
}
