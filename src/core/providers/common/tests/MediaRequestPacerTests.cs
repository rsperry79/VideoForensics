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
    }
}
