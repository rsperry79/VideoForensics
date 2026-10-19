using VideoForensics.Core.Providers.Common.Services;
using Xunit;

namespace VideoForensics.Core.Providers.Common.Tests
{
    /// <summary>Pacing turns on an injected clock, so no test sleeps.</summary>
    public class MediaRequestPacerTests
    {
        private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [Fact]
        public async Task WaitTurnAsync_SecondTurnInsideInterval_WaitsUntilIntervalElapsed()
        {
            DateTime now = Start;
            var starts = new List<DateTime>();
            var pacer = new MediaRequestPacer(() => now, (delay, _) => { now += delay; return Task.CompletedTask; }, TimeSpan.FromSeconds(1));

            await pacer.WaitTurnAsync(CancellationToken.None);
            starts.Add(now);
            await pacer.WaitTurnAsync(CancellationToken.None);
            starts.Add(now);

            Assert.True(starts[1] - starts[0] >= TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task WaitTurnAsync_IntervalAlreadyElapsed_DoesNotDelay()
        {
            DateTime now = Start;
            var delays = new List<TimeSpan>();
            var pacer = new MediaRequestPacer(() => now, (delay, _) => { delays.Add(delay); now += delay; return Task.CompletedTask; }, TimeSpan.FromSeconds(1));

            await pacer.WaitTurnAsync(CancellationToken.None);
            now = now.AddSeconds(5);
            await pacer.WaitTurnAsync(CancellationToken.None);

            Assert.Empty(delays);
        }

        [Fact]
        public void RecordRateLimited_NoRetryAfter_AddsOneSecondToMinInterval()
        {
            var pacer = new MediaRequestPacer(() => Start, (_, _) => Task.CompletedTask, TimeSpan.FromSeconds(1));

            pacer.RecordRateLimited(null);

            Assert.Equal(TimeSpan.FromSeconds(2), pacer.MinInterval);
            Assert.Equal(TimeSpan.FromSeconds(1), pacer.RateLimitPenalty);
        }

        [Fact]
        public void RecordRateLimited_CalledTwice_AddsTwoSecondsToMinInterval()
        {
            var pacer = new MediaRequestPacer(() => Start, (_, _) => Task.CompletedTask, TimeSpan.FromSeconds(1));

            pacer.RecordRateLimited(null);
            pacer.RecordRateLimited(null);

            Assert.Equal(TimeSpan.FromSeconds(3), pacer.MinInterval);
        }

        [Fact]
        public async Task WaitTurnAsync_PauseLongerThanInterval_WaitsForPause()
        {
            DateTime now = Start;
            var delays = new List<TimeSpan>();
            var pacer = new MediaRequestPacer(() => now, (delay, _) => { delays.Add(delay); now += delay; return Task.CompletedTask; }, TimeSpan.FromSeconds(1));

            await pacer.WaitTurnAsync(CancellationToken.None);
            pacer.RecordRateLimited(TimeSpan.FromSeconds(7));
            await pacer.WaitTurnAsync(CancellationToken.None);

            Assert.Equal(TimeSpan.FromSeconds(7), Assert.Single(delays));
            Assert.Equal(Start.AddSeconds(7), now);
        }
    }
}
