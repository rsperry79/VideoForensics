using VideoForensics.Core.Providers.Common.Services;

using Xunit;

namespace VideoForensics.Core.Providers.Common.Tests.Services
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

            _ = pacer.RecordRateLimited(null);

            Assert.Equal(TimeSpan.FromSeconds(2), pacer.MinInterval);
            Assert.Equal(TimeSpan.FromSeconds(1), pacer.RateLimitPenalty);
        }

        [Fact]
        public void RecordRateLimited_TwoConsecutiveCallsInOneIncident_AddsOneSecondAndReturnsTrueThenFalse()
        {
            var pacer = new MediaRequestPacer(() => Start, (_, _) => Task.CompletedTask, TimeSpan.FromSeconds(1));

            bool first = pacer.RecordRateLimited(null);
            bool second = pacer.RecordRateLimited(null);

            Assert.True(first);
            Assert.False(second);
            Assert.Equal(TimeSpan.FromSeconds(2), pacer.MinInterval);
            Assert.Equal(TimeSpan.FromSeconds(1), pacer.RateLimitPenalty);
        }

        [Fact]
        public void RecordRateLimited_AfterMarkRequestSucceeded_AddsAnotherSecondAndReturnsTrue()
        {
            var pacer = new MediaRequestPacer(() => Start, (_, _) => Task.CompletedTask, TimeSpan.FromSeconds(1));
            _ = pacer.RecordRateLimited(null);
            pacer.MarkRequestSucceeded();

            bool newIncident = pacer.RecordRateLimited(null);

            Assert.True(newIncident);
            Assert.Equal(TimeSpan.FromSeconds(3), pacer.MinInterval);
        }

        [Fact]
        public void MarkRequestSucceeded_AfterRateLimit_ClearsIncidentWithoutReducingMinInterval()
        {
            var pacer = new MediaRequestPacer(() => Start, (_, _) => Task.CompletedTask, TimeSpan.FromSeconds(1));
            _ = pacer.RecordRateLimited(null);
            Assert.True(pacer.IsInRateLimitIncident);

            pacer.MarkRequestSucceeded();

            Assert.False(pacer.IsInRateLimitIncident);
            Assert.Equal(TimeSpan.FromSeconds(2), pacer.MinInterval);
        }

        [Fact]
        public async Task RecordRateLimited_SecondCallInIncidentWithLongerRetryAfter_ExtendsPause()
        {
            DateTime now = Start;
            var delays = new List<TimeSpan>();
            var pacer = new MediaRequestPacer(() => now, (delay, _) => { delays.Add(delay); now += delay; return Task.CompletedTask; }, TimeSpan.FromSeconds(1));

            await pacer.WaitTurnAsync(CancellationToken.None);
            _ = pacer.RecordRateLimited(TimeSpan.FromSeconds(7));
            _ = pacer.RecordRateLimited(TimeSpan.FromSeconds(20));
            await pacer.WaitTurnAsync(CancellationToken.None);

            Assert.Equal(TimeSpan.FromSeconds(20), Assert.Single(delays));
            Assert.Equal(Start.AddSeconds(20), now);
        }

        [Fact]
        public async Task RecordRateLimited_SecondCallInIncidentWithShorterRetryAfter_KeepsLongerPause()
        {
            DateTime now = Start;
            var delays = new List<TimeSpan>();
            var pacer = new MediaRequestPacer(() => now, (delay, _) => { delays.Add(delay); now += delay; return Task.CompletedTask; }, TimeSpan.FromSeconds(1));

            await pacer.WaitTurnAsync(CancellationToken.None);
            _ = pacer.RecordRateLimited(TimeSpan.FromSeconds(20));
            _ = pacer.RecordRateLimited(TimeSpan.FromSeconds(3));
            await pacer.WaitTurnAsync(CancellationToken.None);

            Assert.Equal(TimeSpan.FromSeconds(20), Assert.Single(delays));
            Assert.Equal(Start.AddSeconds(20), now);
        }

        [Fact]
        public async Task WaitTurnAsync_PauseLongerThanInterval_WaitsForPause()
        {
            DateTime now = Start;
            var delays = new List<TimeSpan>();
            var pacer = new MediaRequestPacer(() => now, (delay, _) => { delays.Add(delay); now += delay; return Task.CompletedTask; }, TimeSpan.FromSeconds(1));

            await pacer.WaitTurnAsync(CancellationToken.None);
            _ = pacer.RecordRateLimited(TimeSpan.FromSeconds(7));
            await pacer.WaitTurnAsync(CancellationToken.None);

            Assert.Equal(TimeSpan.FromSeconds(7), Assert.Single(delays));
            Assert.Equal(Start.AddSeconds(7), now);
        }

        [Fact]
        public async Task RecordHardBan_FirstHardBan_PausesFifteenMinutes()
        {
            DateTime now = Start;
            var delays = new List<TimeSpan>();
            var pacer = new MediaRequestPacer(() => now, (delay, _) => { delays.Add(delay); now += delay; return Task.CompletedTask; }, TimeSpan.FromSeconds(1));

            _ = pacer.RecordHardBan(null);
            await pacer.WaitTurnAsync(CancellationToken.None);

            Assert.Equal(TimeSpan.FromMinutes(15), Assert.Single(delays));
        }

        [Fact]
        public void RecordHardBan_SecondHardBanBeforeSuccess_PausesThirtyMinutes()
        {
            var pacer = new MediaRequestPacer(() => Start, (_, _) => Task.CompletedTask, TimeSpan.FromSeconds(1));
            _ = pacer.RecordHardBan(null);

            MediaRequestPacer.HardBanOutcome second = pacer.RecordHardBan(null);

            Assert.Equal(TimeSpan.FromMinutes(30), second.Pause);
        }

        [Fact]
        public void RecordHardBan_HardBanAfterSuccess_PausesFifteenMinutesAgain()
        {
            DateTime now = Start;
            var pacer = new MediaRequestPacer(() => now, (_, _) => Task.CompletedTask, TimeSpan.FromSeconds(1));
            _ = pacer.RecordHardBan(null);
            pacer.MarkRequestSucceeded();
            now = Start.AddHours(1);

            MediaRequestPacer.HardBanOutcome again = pacer.RecordHardBan(null);

            Assert.Equal(TimeSpan.FromMinutes(15), again.Pause);
        }

        [Fact]
        public void RecordHardBan_Always_AddsNoSpacingPenalty()
        {
            var pacer = new MediaRequestPacer(() => Start, (_, _) => Task.CompletedTask, TimeSpan.FromSeconds(1));

            _ = pacer.RecordHardBan(null);
            _ = pacer.RecordHardBan(null);

            Assert.Equal(TimeSpan.FromSeconds(1), pacer.MinInterval);
            Assert.Equal(TimeSpan.Zero, pacer.RateLimitPenalty);
        }

        [Fact]
        public void RecordHardBan_RetryAfterLongerThanRule_UsesRetryAfter()
        {
            var pacer = new MediaRequestPacer(() => Start, (_, _) => Task.CompletedTask, TimeSpan.FromSeconds(1));

            MediaRequestPacer.HardBanOutcome outcome = pacer.RecordHardBan(TimeSpan.FromMinutes(45));

            Assert.Equal(TimeSpan.FromMinutes(45), outcome.Pause);
        }

        [Fact]
        public void RecordHardBan_RetryAfterShorterThanRule_UsesRule()
        {
            var pacer = new MediaRequestPacer(() => Start, (_, _) => Task.CompletedTask, TimeSpan.FromSeconds(1));

            MediaRequestPacer.HardBanOutcome outcome = pacer.RecordHardBan(TimeSpan.FromMinutes(5));

            Assert.Equal(TimeSpan.FromMinutes(15), outcome.Pause);
        }

        [Fact]
        public async Task RecordHardBan_ShorterThanExistingPause_KeepsLongerPause()
        {
            DateTime now = Start;
            var delays = new List<TimeSpan>();
            var pacer = new MediaRequestPacer(() => now, (delay, _) => { delays.Add(delay); now += delay; return Task.CompletedTask; }, TimeSpan.FromSeconds(1));
            _ = pacer.RecordRateLimited(TimeSpan.FromMinutes(60));

            _ = pacer.RecordHardBan(null);
            await pacer.WaitTurnAsync(CancellationToken.None);

            Assert.Equal(TimeSpan.FromMinutes(60), Assert.Single(delays));
        }

        [Fact]
        public void RecordHardBan_FirstHardBanInWindow_RaisesAlert()
        {
            var pacer = new MediaRequestPacer(() => Start, (_, _) => Task.CompletedTask, TimeSpan.FromSeconds(1));

            MediaRequestPacer.HardBanOutcome outcome = pacer.RecordHardBan(null);

            Assert.True(outcome.AlertRaised);
        }

        [Fact]
        public void RecordHardBan_RepeatWhileWindowActive_DoesNotRaiseAlert()
        {
            DateTime now = Start;
            var pacer = new MediaRequestPacer(() => now, (_, _) => Task.CompletedTask, TimeSpan.FromSeconds(1));
            _ = pacer.RecordHardBan(null);
            now = Start.AddMinutes(5);

            MediaRequestPacer.HardBanOutcome repeat = pacer.RecordHardBan(null);

            Assert.False(repeat.AlertRaised);
        }

        [Fact]
        public void RecordHardBan_AfterWindowExpired_RaisesAlertAgain()
        {
            DateTime now = Start;
            var pacer = new MediaRequestPacer(() => now, (_, _) => Task.CompletedTask, TimeSpan.FromSeconds(1));
            _ = pacer.RecordHardBan(null);
            now = Start.AddMinutes(31);

            MediaRequestPacer.HardBanOutcome next = pacer.RecordHardBan(null);

            Assert.True(next.AlertRaised);
        }

        [Fact]
        public void RecordHardBan_AfterNormalRateLimitInSameIncident_StillRaisesHardBanAlert()
        {
            var pacer = new MediaRequestPacer(() => Start, (_, _) => Task.CompletedTask, TimeSpan.FromSeconds(1));
            _ = pacer.RecordRateLimited(null);

            MediaRequestPacer.HardBanOutcome outcome = pacer.RecordHardBan(null);

            Assert.True(outcome.AlertRaised);
        }

        [Fact]
        public void RecordHardBan_Always_MarksRateLimitIncident()
        {
            var pacer = new MediaRequestPacer(() => Start, (_, _) => Task.CompletedTask, TimeSpan.FromSeconds(1));

            _ = pacer.RecordHardBan(null);

            Assert.True(pacer.IsInRateLimitIncident);
        }

        [Fact]
        public void RecordRateLimited_AfterHardBanBeforeSuccess_IsSameIncidentWithNoSpacing()
        {
            var pacer = new MediaRequestPacer(() => Start, (_, _) => Task.CompletedTask, TimeSpan.FromSeconds(1));
            _ = pacer.RecordHardBan(null);

            bool newIncident = pacer.RecordRateLimited(null);

            Assert.False(newIncident);
            Assert.Equal(TimeSpan.FromSeconds(1), pacer.MinInterval);
            Assert.Equal(TimeSpan.Zero, pacer.RateLimitPenalty);
        }

        [Fact]
        public void MarkRequestSucceeded_AfterHardBan_EndsIncident()
        {
            var pacer = new MediaRequestPacer(() => Start, (_, _) => Task.CompletedTask, TimeSpan.FromSeconds(1));
            _ = pacer.RecordHardBan(null);

            pacer.MarkRequestSucceeded();

            Assert.False(pacer.IsInRateLimitIncident);
            Assert.True(pacer.RecordRateLimited(null));
        }
    }
}
