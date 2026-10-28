using System.Reflection;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using VideoForensics.Client.Common.Contracts;
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

        private static LocalDownloadProgressSource BuildSource(ISyncActivityLog log)
        {
            return new LocalDownloadProgressSource(log, Microsoft.Extensions.Logging.Abstractions.NullLogger<LocalDownloadProgressSource>.Instance, NeverTicks);
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

        [Fact]
        public void DownloadProgressSources_Constructors_DoNotTakeDownloadService()
        {
            // The live progress path reads only the sync activity log. A constructor that still took
            // IVideoDownloadService would reintroduce the second, never-updating data source.
            Type[] types = [typeof(LocalDownloadProgressSource), typeof(LiveHub), typeof(DownloadProgressBroadcastService)];

            foreach (Type type in types)
            {
                foreach (ConstructorInfo constructor in type.GetConstructors())
                {
                    Assert.DoesNotContain(
                        constructor.GetParameters(),
                        parameter => parameter.ParameterType == typeof(IVideoDownloadService));
                }
            }
        }
    }
}
