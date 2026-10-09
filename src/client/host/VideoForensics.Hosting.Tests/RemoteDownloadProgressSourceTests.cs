using System.Reactive.Subjects;

using Moq;

using VideoForensics.Api.Contracts;
using VideoForensics.Hosting.Contracts;
using VideoForensics.Hosting.Remote;
using VideoForensics.Ui.Shared.Contracts;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class RemoteDownloadProgressSourceTests
    {
        private static DownloadProgressDto BuildDto(IReadOnlyList<string>? activity = null)
        {
            var status = new DownloadStatusDto(
                IsDownloading: true,
                FilesCompleted: 2,
                FilesTotal: 5,
                BytesDownloaded: 1024L,
                CurrentFile: "clip.mp4",
                TotalFilesCompleted: 7,
                TotalFilesMatched: 20,
                TotalBytesDownloaded: 4096L,
                ActiveConnections: 3,
                CurrentSpeedMbps: 1.25);
            return new DownloadProgressDto(
                status,
                CurrentDeviceIndex: 2,
                CurrentDeviceTotal: 4,
                CurrentDeviceName: "Garage",
                Activity: activity ?? [],
                PreScanCounts: new Dictionary<string, int> { ["dev-1"] = 9 },
                LastError: "timeout",
                RemainingReason: "Rate limited");
        }

        [Fact]
        public void RemoteDownloadProgressSource_Progress_MapsEveryDtoFieldToSnapshot()
        {
            var subject = new Subject<DownloadProgressDto>();
            var store = new Mock<IRealtimeStore>();
            store.SetupGet(s => s.DownloadProgress).Returns(subject);
            store.SetupGet(s => s.LatestDownloadProgress).Returns((DownloadProgressDto?)null);
            var source = new RemoteDownloadProgressSource(store.Object);

            DownloadProgressSnapshot? received = null;
            using IDisposable subscription = source.Progress.Subscribe(s => received = s);
            subject.OnNext(BuildDto(["line one"]));

            Assert.NotNull(received);
            Assert.True(received!.IsDownloading);
            Assert.Equal(2, received.FilesCompleted);
            Assert.Equal(5, received.FilesTotal);
            Assert.Equal(1024L, received.BytesDownloaded);
            Assert.Equal("clip.mp4", received.CurrentFile);
            Assert.Equal(7, received.TotalFilesCompleted);
            Assert.Equal(20, received.TotalFilesMatched);
            Assert.Equal(4096L, received.TotalBytesDownloaded);
            Assert.Equal(3, received.ActiveConnections);
            Assert.Equal(1.25, received.CurrentSpeedMbps);
            Assert.Equal(2, received.CurrentDeviceIndex);
            Assert.Equal(4, received.CurrentDeviceTotal);
            Assert.Equal("Garage", received.CurrentDeviceName);
            Assert.Equal(9, received.PreScanCounts["dev-1"]);
            Assert.Equal(new[] { "line one" }, received.Activity);
            Assert.Equal("timeout", received.LastError);
            Assert.Equal("Rate limited", received.RemainingReason);
        }

        [Fact]
        public void RemoteDownloadProgressSource_Progress_NullDeviceNameMapsToEmptyString()
        {
            var subject = new Subject<DownloadProgressDto>();
            var store = new Mock<IRealtimeStore>();
            store.SetupGet(s => s.DownloadProgress).Returns(subject);
            store.SetupGet(s => s.LatestDownloadProgress).Returns((DownloadProgressDto?)null);
            var source = new RemoteDownloadProgressSource(store.Object);
            DownloadProgressDto dto = BuildDto() with { CurrentDeviceName = null };

            DownloadProgressSnapshot? received = null;
            using IDisposable subscription = source.Progress.Subscribe(s => received = s);
            subject.OnNext(dto);

            Assert.Equal(string.Empty, received!.CurrentDeviceName);
        }

        [Fact]
        public void RemoteDownloadProgressSource_Progress_ReplayedLatestValueDoesNotRepeatActivity()
        {
            // The store replays its latest value to a new subscriber. Its activity lines were already
            // shown when they first arrived, so the replay must not append them to the panel again.
            DownloadProgressDto latest = BuildDto(["already shown"]);
            var subject = new BehaviorSubject<DownloadProgressDto>(latest);
            var store = new Mock<IRealtimeStore>();
            store.SetupGet(s => s.DownloadProgress).Returns(subject);
            store.SetupGet(s => s.LatestDownloadProgress).Returns(latest);
            var source = new RemoteDownloadProgressSource(store.Object);

            var received = new List<DownloadProgressSnapshot>();
            using IDisposable subscription = source.Progress.Subscribe(s => received.Add(s));

            Assert.Single(received);
            Assert.Empty(received[0].Activity);
            Assert.Equal(2, received[0].FilesCompleted);
        }

        [Fact]
        public void RemoteDownloadProgressSource_Progress_LiveEmissionAfterReplayKeepsActivity()
        {
            DownloadProgressDto latest = BuildDto(["already shown"]);
            var subject = new BehaviorSubject<DownloadProgressDto>(latest);
            var store = new Mock<IRealtimeStore>();
            store.SetupGet(s => s.DownloadProgress).Returns(subject);
            store.SetupGet(s => s.LatestDownloadProgress).Returns(latest);
            var source = new RemoteDownloadProgressSource(store.Object);

            var received = new List<DownloadProgressSnapshot>();
            using IDisposable subscription = source.Progress.Subscribe(s => received.Add(s));
            subject.OnNext(BuildDto(["fresh line"]));

            Assert.Equal(2, received.Count);
            Assert.Equal(new[] { "fresh line" }, received[1].Activity);
        }

        [Fact]
        public void RemoteDownloadProgressSource_Progress_NeverDrainsStoreActivityLog()
        {
            var subject = new Subject<DownloadProgressDto>();
            var store = new Mock<IRealtimeStore>();
            store.SetupGet(s => s.DownloadProgress).Returns(subject);
            store.SetupGet(s => s.LatestDownloadProgress).Returns((DownloadProgressDto?)null);
            var source = new RemoteDownloadProgressSource(store.Object);

            using IDisposable subscription = source.Progress.Subscribe(_ => { });
            subject.OnNext(BuildDto(["line"]));

            // Draining is destructive and shared. The panel gets lines from each emission instead.
            store.Verify(s => s.DrainActivityLog(), Times.Never);
        }

        [Fact]
        public void RemoteDownloadProgressSource_Dispose_RemovesSubscriptionFromStore()
        {
            var subject = new Subject<DownloadProgressDto>();
            var store = new Mock<IRealtimeStore>();
            store.SetupGet(s => s.DownloadProgress).Returns(subject);
            store.SetupGet(s => s.LatestDownloadProgress).Returns((DownloadProgressDto?)null);
            var source = new RemoteDownloadProgressSource(store.Object);

            IDisposable subscription = source.Progress.Subscribe(_ => { });
            Assert.True(subject.HasObservers);

            subscription.Dispose();

            Assert.False(subject.HasObservers);
        }
    }
}
