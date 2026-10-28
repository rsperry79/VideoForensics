using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Client.Hosting.Contracts;
using VideoForensics.Client.Hosting.Services;

namespace VideoForensics.Client.Hosting.Tests
{
    public class DownloadProgressChangeDetectorTests
    {
        private static DownloadProgressDto Build(
            int filesCompleted = 1,
            int index = 1,
            IReadOnlyList<string>? activity = null,
            IReadOnlyDictionary<string, int>? preScan = null,
            string? lastError = null)
        {
            return new DownloadProgressDto(
                new DownloadStatusDto(true, filesCompleted, 4, 100L, "a.mp4"),
                index,
                3,
                "Garage",
                activity ?? Array.Empty<string>(),
                preScan ?? new Dictionary<string, int>(),
                lastError,
                null);
        }

        [Fact]
        public void DownloadProgressChangeDetector_UnchangedStateNoActivity_NotSent()
        {
            IDownloadProgressChangeDetector detector = new DownloadProgressChangeDetector();
            DownloadProgressDto previous = Build();
            DownloadProgressDto current = Build();

            Assert.False(detector.ShouldSend(previous, current));
        }

        [Fact]
        public void DownloadProgressChangeDetector_ChangedProgress_Sent()
        {
            IDownloadProgressChangeDetector detector = new DownloadProgressChangeDetector();

            Assert.True(detector.ShouldSend(Build(filesCompleted: 1), Build(filesCompleted: 2)));
        }

        [Fact]
        public void DownloadProgressChangeDetector_ActivityNonEmptyButStateUnchanged_Sent()
        {
            IDownloadProgressChangeDetector detector = new DownloadProgressChangeDetector();
            DownloadProgressDto previous = Build();
            DownloadProgressDto current = Build(activity: new[] { "✓ file.mp4 (5.2 MB)" });

            Assert.True(detector.ShouldSend(previous, current));
        }

        [Fact]
        public void DownloadProgressChangeDetector_ActivityOnlyDifference_HasChangedFalse()
        {
            IDownloadProgressChangeDetector detector = new DownloadProgressChangeDetector();

            Assert.False(detector.HasChanged(Build(), Build(activity: new[] { "line" })));
        }

        [Fact]
        public void DownloadProgressChangeDetector_PreScanCountsSameContentNewInstance_NotChanged()
        {
            IDownloadProgressChangeDetector detector = new DownloadProgressChangeDetector();
            DownloadProgressDto previous = Build(preScan: new Dictionary<string, int> { ["dev-1"] = 6, ["dev-2"] = 2 });
            DownloadProgressDto current = Build(preScan: new Dictionary<string, int> { ["dev-2"] = 2, ["dev-1"] = 6 });

            Assert.False(detector.HasChanged(previous, current));
        }

        [Fact]
        public void DownloadProgressChangeDetector_PreScanCountChanged_Sent()
        {
            IDownloadProgressChangeDetector detector = new DownloadProgressChangeDetector();
            DownloadProgressDto previous = Build(preScan: new Dictionary<string, int> { ["dev-1"] = 6 });
            DownloadProgressDto current = Build(preScan: new Dictionary<string, int> { ["dev-1"] = 7 });

            Assert.True(detector.ShouldSend(previous, current));
        }

        [Fact]
        public void DownloadProgressChangeDetector_NoPreviousSnapshot_Sent()
        {
            IDownloadProgressChangeDetector detector = new DownloadProgressChangeDetector();

            Assert.True(detector.ShouldSend(null, Build()));
        }

        [Fact]
        public void DownloadProgressChangeDetector_IdleSnapshotRepeated_NotSent()
        {
            IDownloadProgressChangeDetector detector = new DownloadProgressChangeDetector();

            Assert.False(detector.ShouldSend(DownloadProgressDtoMapping.IdleSnapshot(), DownloadProgressDtoMapping.IdleSnapshot()));
        }

        [Fact]
        public void DownloadProgressChangeDetector_IdleSnapshotWithOnlySyncLines_Sent()
        {
            IDownloadProgressChangeDetector detector = new DownloadProgressChangeDetector();
            var log = new Mock<ISyncActivityLog>();
            _ = log.Setup(l => l.Drain()).Returns(["sync started"]);
            DownloadProgressDto withLines = log.Object.ToDownloadProgressDto();

            Assert.True(detector.ShouldSend(DownloadProgressDtoMapping.IdleSnapshot(), withLines));
        }

        [Fact]
        public void DownloadProgressChangeDetector_StatusChangedFromStore_Sent()
        {
            IDownloadProgressChangeDetector detector = new DownloadProgressChangeDetector();
            var store = new SyncProgressStore();
            DownloadProgressDto idle = DownloadProgressDtoMapping.IdleSnapshot(store);
            store.Set(new SyncProgress(true, 5, 1, 100L, "a.mp4", 1, 1, 2, "Garage"));
            DownloadProgressDto downloading = DownloadProgressDtoMapping.IdleSnapshot(store);

            Assert.True(detector.HasChanged(idle, downloading));
            Assert.True(detector.ShouldSend(idle, downloading));
        }

        [Fact]
        public void DownloadProgressChangeDetector_DeviceChangedOnly_Sent()
        {
            IDownloadProgressChangeDetector detector = new DownloadProgressChangeDetector();
            var store = new SyncProgressStore();
            store.Set(new SyncProgress(true, 5, 1, 100L, "a.mp4", 1, 1, 2, "Garage"));
            DownloadProgressDto first = DownloadProgressDtoMapping.IdleSnapshot(store);
            store.Set(new SyncProgress(true, 5, 1, 100L, "a.mp4", 1, 2, 2, "Porch"));
            DownloadProgressDto second = DownloadProgressDtoMapping.IdleSnapshot(store);

            Assert.True(detector.ShouldSend(first, second));
        }

        [Fact]
        public void DownloadProgressChangeDetector_IdenticalStoreProgressRepeated_NotSent()
        {
            IDownloadProgressChangeDetector detector = new DownloadProgressChangeDetector();
            var store = new SyncProgressStore();
            store.Set(new SyncProgress(true, 5, 1, 100L, "a.mp4", 1, 1, 2, "Garage"));

            Assert.False(detector.ShouldSend(DownloadProgressDtoMapping.IdleSnapshot(store), DownloadProgressDtoMapping.IdleSnapshot(store)));
        }
    }
}
