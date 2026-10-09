using VideoForensics.Api.Contracts;
using VideoForensics.Hosting.Contracts;

using Xunit;

namespace VideoForensics.Hosting.Tests
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
    }
}
