using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting.Contracts;
using VideoForensics.Client.Hosting.Services;
using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Client.WebApp.Hubs;
using VideoForensics.Client.WebApp.Services;
using VideoForensics.Core.Providers.Common.Contracts;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Services
{
    public class LocalDownloadProgressSourceTests
    {
        // A period far longer than any test runs, so only the explicit SampleOnce calls sample.
        private static readonly TimeSpan NeverTicks = TimeSpan.FromHours(1);

        /// <summary>Fake sync log: lines a test appends are returned by the next Drain, and each Drain is counted.</summary>
        private sealed class FakeActivityLog : ISyncActivityLog
        {
            public List<string> Pending { get; } = [];

            public int DrainCount { get; private set; }

            public void Append(string line)
            {
                Pending.Add(line);
            }

            public IReadOnlyList<string> Drain()
            {
                DrainCount++;
                string[] lines = [.. Pending];
                Pending.Clear();
                return lines;
            }
        }

        private static LocalDownloadProgressSource BuildSource(ISyncActivityLog log, ISyncProgressStore? store = null)
        {
            return new LocalDownloadProgressSource(log, store ?? new SyncProgressStore(), Microsoft.Extensions.Logging.Abstractions.NullLogger<LocalDownloadProgressSource>.Instance, NeverTicks);
        }

        [Fact]
        public void LocalDownloadProgressSource_SampleOnce_ChangedStoreValueEmitsWithoutActivity()
        {
            var store = new SyncProgressStore();
            using LocalDownloadProgressSource source = BuildSource(new FakeActivityLog(), store);
            var received = new List<DownloadProgressSnapshot>();
            using IDisposable subscription = source.Progress.Subscribe(received.Add);
            source.SampleOnce();

            store.Set(new SyncProgress(true, 5, 3, 2048L, "a.mp4", 2, 1, 4, "Garage"));
            source.SampleOnce();

            Assert.Equal(2, received.Count);
            Assert.True(received[1].IsDownloading);
            Assert.Equal(3, received[1].FilesCompleted);
            Assert.Equal(5, received[1].FilesTotal);
            Assert.Equal("a.mp4", received[1].CurrentFile);
            Assert.Equal(1, received[1].CurrentDeviceIndex);
            Assert.Equal(4, received[1].CurrentDeviceTotal);
            Assert.Equal("Garage", received[1].CurrentDeviceName);
            Assert.Empty(received[1].Activity);
        }

        [Fact]
        public void LocalDownloadProgressSource_SampleOnce_UnchangedStoreValueDoesNotReEmit()
        {
            var store = new SyncProgressStore();
            store.Set(new SyncProgress(true, 5, 3, 2048L, "a.mp4", 2, 1, 4, "Garage"));
            using LocalDownloadProgressSource source = BuildSource(new FakeActivityLog(), store);
            var received = new List<DownloadProgressSnapshot>();
            using IDisposable subscription = source.Progress.Subscribe(received.Add);
            source.SampleOnce();

            source.SampleOnce();
            source.SampleOnce();

            _ = Assert.Single(received);
        }

        [Fact]
        public void LocalDownloadProgressSource_SampleOnce_EmitsFirstSampleAsIdle()
        {
            using LocalDownloadProgressSource source = BuildSource(new FakeActivityLog());
            var received = new List<DownloadProgressSnapshot>();
            using IDisposable subscription = source.Progress.Subscribe(received.Add);

            source.SampleOnce();

            _ = Assert.Single(received);
            Assert.False(received[0].IsDownloading);
            Assert.Equal(0, received[0].FilesCompleted);
            Assert.Equal(0, received[0].CurrentDeviceTotal);
            Assert.Equal(string.Empty, received[0].CurrentDeviceName);
            Assert.Empty(received[0].PreScanCounts);
            Assert.Null(received[0].LastError);
            Assert.Null(received[0].RemainingReason);
            Assert.Empty(received[0].Activity);
        }

        [Fact]
        public void LocalDownloadProgressSource_SampleOnce_EmptyTicksDoNotEmit()
        {
            // With no activity and no state to compare, repeated idle ticks must not re-broadcast.
            using LocalDownloadProgressSource source = BuildSource(new FakeActivityLog());
            var received = new List<DownloadProgressSnapshot>();
            using IDisposable subscription = source.Progress.Subscribe(received.Add);
            source.SampleOnce();

            source.SampleOnce();
            source.SampleOnce();

            _ = Assert.Single(received);
        }

        [Fact]
        public void LocalDownloadProgressSource_SampleOnce_ActivityEmitsEvenWhenNothingElseChanged()
        {
            var log = new FakeActivityLog();
            using LocalDownloadProgressSource source = BuildSource(log);
            var received = new List<DownloadProgressSnapshot>();
            using IDisposable subscription = source.Progress.Subscribe(received.Add);
            source.SampleOnce();

            log.Append("file.mp4 done");
            source.SampleOnce();

            Assert.Equal(2, received.Count);
            Assert.Equal(new[] { "file.mp4 done" }, received[1].Activity);
        }

        [Fact]
        public void LocalDownloadProgressSource_SampleOnce_ActivityIsDrainedOncePerSample()
        {
            var log = new FakeActivityLog();
            using LocalDownloadProgressSource source = BuildSource(log);
            var received = new List<DownloadProgressSnapshot>();
            using IDisposable subscription = source.Progress.Subscribe(received.Add);
            source.SampleOnce();
            log.Append("line");

            source.SampleOnce();
            source.SampleOnce();

            Assert.Equal(3, log.DrainCount);
            Assert.Equal(2, received.Count);
            Assert.Equal(new[] { "line" }, received[1].Activity);
        }

        [Fact]
        public void LocalDownloadProgressSource_Progress_NewSubscriberReceivesLatestSnapshot()
        {
            using LocalDownloadProgressSource source = BuildSource(new FakeActivityLog());
            source.SampleOnce();

            var received = new List<DownloadProgressSnapshot>();
            using IDisposable subscription = source.Progress.Subscribe(received.Add);

            _ = Assert.Single(received);
            Assert.False(received[0].IsDownloading);
        }

        [Fact]
        public void LocalDownloadProgressSource_SampleOnce_LogThrows_EmitsNothingAndDoesNotThrow()
        {
            var log = new Mock<ISyncActivityLog>();
            _ = log.Setup(l => l.Drain()).Throws(new InvalidOperationException("log unavailable"));
            using LocalDownloadProgressSource source = BuildSource(log.Object);
            var received = new List<DownloadProgressSnapshot>();
            using IDisposable subscription = source.Progress.Subscribe(received.Add);

            source.SampleOnce();

            Assert.Empty(received);
        }

        [Fact]
        public void LocalDownloadProgressSource_Dispose_CompletesSubscribers()
        {
            LocalDownloadProgressSource source = BuildSource(new FakeActivityLog());
            bool completed = false;
            using IDisposable subscription = source.Progress.Subscribe(_ => { }, () => completed = true);

            source.Dispose();

            Assert.True(completed);
        }
    }
}
