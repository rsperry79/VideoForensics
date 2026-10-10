using System.Diagnostics;
using System.Reactive.Subjects;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Contracts;
using VideoForensics.Ui.Shared.Services;

using Xunit;

namespace VideoForensics.Ui.Shared.Tests.Services
{
    public class LiveViewActivationWaiterTests
    {
        private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(300);
        private static readonly TimeSpan FallbackTimeout = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan LongTimeout = TimeSpan.FromSeconds(5);

        /// <summary>Source whose emissions the test drives. Can be told to fail on subscribe.</summary>
        private sealed class FakeSessionSource : ILiveViewSessionSource
        {
            public Subject<LiveViewSession> Changes { get; } = new();

            public bool IsConnected { get; set; } = true;

            public int SubscribeCalls { get; private set; }

            /// <summary>The session id from the most recent SubscribeAsync call.</summary>
            public Guid? LastSubscribedId { get; private set; }

            public Exception? SubscribeFailure { get; set; }

            /// <summary>Runs after a successful subscribe, so a test can emit the push the server would send.</summary>
            public Action<Guid>? OnSubscribed { get; set; }

            public IObservable<LiveViewSession> SessionChanged => Changes;

            public IObservable<LiveViewTelemetrySample> Telemetry { get; } = new Subject<LiveViewTelemetrySample>();

            public Task SubscribeAsync(Guid sessionId, CancellationToken cancellationToken)
            {
                SubscribeCalls++;
                LastSubscribedId = sessionId;
                if (SubscribeFailure is not null)
                {
                    return Task.FromException(SubscribeFailure);
                }

                OnSubscribed?.Invoke(sessionId);
                return Task.CompletedTask;
            }

            public Task UnsubscribeAsync(Guid sessionId, CancellationToken cancellationToken) => Task.CompletedTask;
        }

        /// <summary>Stand-in for GetActiveSessionAsync. Each call returns the result for its 1-based call number.</summary>
        private sealed class CountingPoll
        {
            private readonly Func<int, LiveViewSession?> _result;

            public CountingPoll(Func<int, LiveViewSession?> result)
            {
                _result = result;
            }

            public int Calls { get; private set; }

            public Task<LiveViewSession?> InvokeAsync(CancellationToken cancellationToken)
            {
                Calls++;
                return Task.FromResult(_result(Calls));
            }
        }

        private static LiveViewSession Session(Guid id, LiveViewSessionState state) => new()
        {
            Id = id,
            DeviceId = Guid.NewGuid(),
            TriggerReason = LiveViewTriggerReason.Manual,
            State = state,
            StartedAtUtc = DateTime.UtcNow,
            LastExtendedAtUtc = DateTime.UtcNow,
        };

        [Fact]
        public async Task LiveViewActivationWaiter_SourceEmitsActive_ReturnsWithoutPolling()
        {
            var sessionId = Guid.NewGuid();
            var source = new FakeSessionSource();
            source.OnSubscribed = id => source.Changes.OnNext(Session(id, LiveViewSessionState.Active));
            var poll = new CountingPoll(_ => null);
            var waiter = new LiveViewActivationWaiter();

            LiveViewSession? result = await waiter.WaitForActiveAsync(sessionId, source, poll.InvokeAsync, ShortTimeout, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(sessionId, result.Id);
            Assert.Equal(LiveViewSessionState.Active, result.State);
            // Only the one race-guard read happens; the push answered the wait, so the timeout poll never ran.
            Assert.Equal(1, poll.Calls);
        }

        [Fact]
        public async Task LiveViewActivationWaiter_AlreadyActiveOnRace_ReturnsWithoutWaiting()
        {
            var sessionId = Guid.NewGuid();
            var source = new FakeSessionSource();
            var poll = new CountingPoll(_ => Session(sessionId, LiveViewSessionState.Active));
            var waiter = new LiveViewActivationWaiter();
            var stopwatch = Stopwatch.StartNew();

            LiveViewSession? result = await waiter.WaitForActiveAsync(sessionId, source, poll.InvokeAsync, LongTimeout, CancellationToken.None);

            stopwatch.Stop();
            Assert.NotNull(result);
            Assert.Equal(LiveViewSessionState.Active, result.State);
            Assert.Equal(1, source.SubscribeCalls);
            Assert.Equal(1, poll.Calls);
            Assert.True(stopwatch.Elapsed < LongTimeout, "The wait should return on the race-guard read, not run to the timeout.");
        }

        [Fact]
        public async Task LiveViewActivationWaiter_AlreadyActive_StillSubscribesBeforeReturning()
        {
            // An already-Active session must still join the push group, or telemetry never arrives for it.
            var sessionId = Guid.NewGuid();
            var source = new FakeSessionSource();
            var poll = new CountingPoll(_ => Session(sessionId, LiveViewSessionState.Active));
            var waiter = new LiveViewActivationWaiter();

            LiveViewSession? result = await waiter.WaitForActiveAsync(sessionId, source, poll.InvokeAsync, LongTimeout, CancellationToken.None);

            Assert.Equal(1, source.SubscribeCalls);
            Assert.Equal(sessionId, source.LastSubscribedId);
            Assert.NotNull(result);
            Assert.Equal(sessionId, result.Id);
            Assert.Equal(LiveViewSessionState.Active, result.State);
        }

        [Fact]
        public async Task LiveViewActivationWaiter_SourceNotConnected_FallsBackToPolling()
        {
            var sessionId = Guid.NewGuid();
            var source = new FakeSessionSource { IsConnected = false };
            var poll = new CountingPoll(call => call == 2 ? Session(sessionId, LiveViewSessionState.Active) : null);
            var waiter = new LiveViewActivationWaiter();

            LiveViewSession? result = await waiter.WaitForActiveAsync(sessionId, source, poll.InvokeAsync, FallbackTimeout, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(LiveViewSessionState.Active, result.State);
            Assert.Equal(0, source.SubscribeCalls);
            Assert.Equal(2, poll.Calls);
        }

        [Fact]
        public async Task LiveViewActivationWaiter_SubscribeFails_FallsBackToPolling()
        {
            var sessionId = Guid.NewGuid();
            var source = new FakeSessionSource { SubscribeFailure = new InvalidOperationException("hub rejected") };
            var poll = new CountingPoll(_ => Session(sessionId, LiveViewSessionState.Active));
            var waiter = new LiveViewActivationWaiter();

            LiveViewSession? result = await waiter.WaitForActiveAsync(sessionId, source, poll.InvokeAsync, FallbackTimeout, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(LiveViewSessionState.Active, result.State);
            Assert.Equal(1, source.SubscribeCalls);
            Assert.True(poll.Calls >= 1, "A failed subscribe must fall back to polling, not give up.");
        }

        [Fact]
        public async Task LiveViewActivationWaiter_TimesOut_ReturnsNull()
        {
            var sessionId = Guid.NewGuid();
            var source = new FakeSessionSource();
            var poll = new CountingPoll(_ => null);
            var waiter = new LiveViewActivationWaiter();

            LiveViewSession? result = await waiter.WaitForActiveAsync(sessionId, source, poll.InvokeAsync, ShortTimeout, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task LiveViewActivationWaiter_EventForOtherSession_IsIgnored()
        {
            var sessionId = Guid.NewGuid();
            var otherSessionId = Guid.NewGuid();
            var source = new FakeSessionSource();
            source.OnSubscribed = _ => source.Changes.OnNext(Session(otherSessionId, LiveViewSessionState.Active));
            var poll = new CountingPoll(_ => null);
            var waiter = new LiveViewActivationWaiter();

            LiveViewSession? result = await waiter.WaitForActiveAsync(sessionId, source, poll.InvokeAsync, ShortTimeout, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task LiveViewActivationWaiter_Cancelled_ThrowsOperationCanceled()
        {
            var sessionId = Guid.NewGuid();
            var source = new FakeSessionSource();
            var poll = new CountingPoll(_ => null);
            var waiter = new LiveViewActivationWaiter();
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                waiter.WaitForActiveAsync(sessionId, source, poll.InvokeAsync, LongTimeout, cancellation.Token));
        }
    }
}
