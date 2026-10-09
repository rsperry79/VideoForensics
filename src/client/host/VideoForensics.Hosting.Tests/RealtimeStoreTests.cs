using System.Reactive.Subjects;

using VideoForensics.Api.Contracts;
using VideoForensics.Hosting.Contracts;
using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.Hosting.Tests
{
    public class RealtimeStoreTests
    {
        /// <summary>Hub double whose streams are plain subjects the test drives directly.</summary>
        private sealed class FakeRealtimeHub : IRealtimeHub
        {
            public Subject<DownloadProgressDto> Progress { get; } = new();
            public Subject<NotificationEvent> Urgent { get; } = new();
            public Subject<SelfTestStatusDto> SelfTest { get; } = new();
            public Subject<ConnectionState> State { get; } = new();

            public IObservable<DownloadProgressDto> DownloadProgress => Progress;
            public IObservable<NotificationEvent> UrgentEvents => Urgent;
            public IObservable<SelfTestStatusDto> SelfTestStatus => SelfTest;
            public IObservable<ConnectionState> Connection => State;
            public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
            public Task StopAsync() => Task.CompletedTask;
        }

        [Fact]
        public void RealtimeStore_LateSubscriber_ReceivesLatestValue()
        {
            var hub = new FakeRealtimeHub();
            using var store = new RealtimeStore(hub);
            DownloadProgressDto first = CreateProgress(lastError: "first");
            DownloadProgressDto latest = CreateProgress(lastError: "latest");

            hub.Progress.OnNext(first);
            hub.Progress.OnNext(latest);

            var received = new List<DownloadProgressDto>();
            using IDisposable subscription = store.DownloadProgress.Subscribe(received.Add);

            Assert.Equal(new[] { latest }, received);
        }

        [Fact]
        public void RealtimeStore_BeforeAnyEmission_ReturnsNull()
        {
            var hub = new FakeRealtimeHub();
            using var store = new RealtimeStore(hub);
            var received = new List<DownloadProgressDto>();
            using IDisposable subscription = store.DownloadProgress.Subscribe(received.Add);

            Assert.Null(store.LatestDownloadProgress);
            Assert.Null(store.LatestUrgentEvent);
            Assert.Null(store.LatestSelfTestStatus);
            Assert.Null(store.LatestConnectionState);
            Assert.Empty(received);
        }

        [Fact]
        public void RealtimeStore_HubEmission_ForwardsEachStream()
        {
            var hub = new FakeRealtimeHub();
            using var store = new RealtimeStore(hub);
            DownloadProgressDto progress = CreateProgress(lastError: "Disk full");
            var urgent = new NotificationEvent("SecurityAlert", DateTime.UtcNow, null, null, null, "Tamper detected");
            var selfTest = new SelfTestStatusDto(SelfTestRunStatus.Idle);

            var seenProgress = new List<DownloadProgressDto>();
            var seenUrgent = new List<NotificationEvent>();
            var seenSelfTest = new List<SelfTestStatusDto>();
            var seenState = new List<ConnectionState>();
            using IDisposable a = store.DownloadProgress.Subscribe(seenProgress.Add);
            using IDisposable b = store.UrgentEvents.Subscribe(seenUrgent.Add);
            using IDisposable c = store.SelfTestStatus.Subscribe(seenSelfTest.Add);
            using IDisposable d = store.Connection.Subscribe(seenState.Add);

            hub.Progress.OnNext(progress);
            hub.Urgent.OnNext(urgent);
            hub.SelfTest.OnNext(selfTest);
            hub.State.OnNext(ConnectionState.Connected);

            Assert.Same(progress, store.LatestDownloadProgress);
            Assert.Same(urgent, store.LatestUrgentEvent);
            Assert.Same(selfTest, store.LatestSelfTestStatus);
            Assert.Equal(ConnectionState.Connected, store.LatestConnectionState);
            Assert.Equal(new[] { progress }, seenProgress);
            Assert.Equal(new[] { urgent }, seenUrgent);
            Assert.Equal(new[] { selfTest }, seenSelfTest);
            Assert.Equal(new[] { ConnectionState.Connected }, seenState);
        }

        [Fact]
        public void RealtimeStore_AfterDispose_StopsForwardingHubEmissions()
        {
            var hub = new FakeRealtimeHub();
            var store = new RealtimeStore(hub);
            store.Dispose();

            hub.Progress.OnNext(CreateProgress(lastError: "ignored"));

            Assert.Null(store.LatestDownloadProgress);
        }

        private static DownloadProgressDto CreateProgress(string? lastError = null)
        {
            return new DownloadProgressDto(
                new DownloadStatusDto(false, 0, 0, 0),
                0,
                0,
                null,
                Array.Empty<string>(),
                new Dictionary<string, int>(),
                lastError,
                null);
        }
    }
}
