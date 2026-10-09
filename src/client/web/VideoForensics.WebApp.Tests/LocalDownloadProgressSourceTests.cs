
using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Ui.Shared.Contracts;
using VideoForensics.WebApp.Services;

using Xunit;

namespace VideoForensics.WebApp.Tests
{
    public class LocalDownloadProgressSourceTests
    {
        // A period far longer than any test runs, so only the explicit SampleOnce calls sample.
        private static readonly TimeSpan NeverTicks = TimeSpan.FromHours(1);

        /// <summary>Mutable state the fake service reads from, so a test can change it between samples.</summary>
        private sealed class SampleState
        {
            public DownloadStatus Status { get; set; } = new(true, 1, 4, 100L, "a.mp4", 2, 8, 200L, 1, 0.5);
            public (int Index, int Total, string Name) Device { get; set; } = (1, 3, "Garage");
            public Dictionary<string, int> PreScanCounts { get; } = new() { ["dev-1"] = 6 };
            public string? LastError { get; set; }
            public string? RemainingReason { get; set; }
            public List<string> PendingActivity { get; } = [];
        }

        private static Mock<IVideoDownloadService> BuildService(SampleState state)
        {
            var service = new Mock<IVideoDownloadService>();
            service.Setup(s => s.GetProgress()).Returns(() => state.Status);
            service.Setup(s => s.GetCurrentDevice()).Returns(() => state.Device);
            service.Setup(s => s.GetPreScanCounts()).Returns(() => new Dictionary<string, int>(state.PreScanCounts));
            service.Setup(s => s.GetLastError()).Returns(() => state.LastError);
            service.Setup(s => s.GetRemainingReason()).Returns(() => state.RemainingReason);
            service.Setup(s => s.DrainActivityLog()).Returns(() =>
            {
                List<string> lines = [.. state.PendingActivity];
                state.PendingActivity.Clear();
                return lines;
            });
            return service;
        }

        private static LocalDownloadProgressSource BuildSource(IVideoDownloadService service)
        {
            return new LocalDownloadProgressSource(service, Microsoft.Extensions.Logging.Abstractions.NullLogger<LocalDownloadProgressSource>.Instance, NeverTicks);
        }

        [Fact]
        public void LocalDownloadProgressSource_SampleOnce_EmitsFirstSample()
        {
            var state = new SampleState();
            using LocalDownloadProgressSource source = BuildSource(BuildService(state).Object);
            var received = new List<DownloadProgressSnapshot>();
            using IDisposable subscription = source.Progress.Subscribe(s => received.Add(s));

            source.SampleOnce();

            Assert.Single(received);
            Assert.Equal(1, received[0].FilesCompleted);
            Assert.Equal(3, received[0].CurrentDeviceTotal);
            Assert.Equal("Garage", received[0].CurrentDeviceName);
        }

        [Fact]
        public void LocalDownloadProgressSource_SampleOnce_UnchangedStateDoesNotEmit()
        {
            var state = new SampleState();
            using LocalDownloadProgressSource source = BuildSource(BuildService(state).Object);
            var received = new List<DownloadProgressSnapshot>();
            using IDisposable subscription = source.Progress.Subscribe(s => received.Add(s));
            source.SampleOnce();

            source.SampleOnce();
            source.SampleOnce();

            Assert.Single(received);
        }

        [Fact]
        public void LocalDownloadProgressSource_SampleOnce_ChangedStateEmitsAgain()
        {
            var state = new SampleState();
            using LocalDownloadProgressSource source = BuildSource(BuildService(state).Object);
            var received = new List<DownloadProgressSnapshot>();
            using IDisposable subscription = source.Progress.Subscribe(s => received.Add(s));
            source.SampleOnce();

            state.Status = new DownloadStatus(true, 2, 4, 150L, "b.mp4", 3, 8, 250L, 1, 0.5);
            source.SampleOnce();

            Assert.Equal(2, received.Count);
            Assert.Equal(2, received[1].FilesCompleted);
        }

        [Theory]
        [InlineData("LastError")]
        [InlineData("RemainingReason")]
        [InlineData("Device")]
        public void LocalDownloadProgressSource_SampleOnce_ChangeInCompareFieldEmits(string field)
        {
            var state = new SampleState();
            using LocalDownloadProgressSource source = BuildSource(BuildService(state).Object);
            var received = new List<DownloadProgressSnapshot>();
            using IDisposable subscription = source.Progress.Subscribe(s => received.Add(s));
            source.SampleOnce();

            switch (field)
            {
                case "LastError": state.LastError = "boom"; break;
                case "RemainingReason": state.RemainingReason = "rate limited"; break;
                case "Device": state.Device = (2, 3, "Porch"); break;
            }
            source.SampleOnce();

            Assert.Equal(2, received.Count);
        }

        [Fact]
        public void LocalDownloadProgressSource_SampleOnce_EqualPreScanContentInNewDictionaryDoesNotEmit()
        {
            // The service's GetPreScanCounts returns a fresh dictionary on every call. Comparison must be
            // by content, or every sample would look changed.
            var state = new SampleState();
            using LocalDownloadProgressSource source = BuildSource(BuildService(state).Object);
            var received = new List<DownloadProgressSnapshot>();
            using IDisposable subscription = source.Progress.Subscribe(s => received.Add(s));
            source.SampleOnce();

            source.SampleOnce();

            Assert.Single(received);
        }

        [Fact]
        public void LocalDownloadProgressSource_SampleOnce_ActivityEmitsEvenWhenStateUnchanged()
        {
            var state = new SampleState();
            using LocalDownloadProgressSource source = BuildSource(BuildService(state).Object);
            var received = new List<DownloadProgressSnapshot>();
            using IDisposable subscription = source.Progress.Subscribe(s => received.Add(s));
            source.SampleOnce();

            state.PendingActivity.Add("file.mp4 done");
            source.SampleOnce();

            Assert.Equal(2, received.Count);
            Assert.Equal(new[] { "file.mp4 done" }, received[1].Activity);
        }

        [Fact]
        public void LocalDownloadProgressSource_SampleOnce_ActivityIsDrainedOncePerSample()
        {
            var state = new SampleState();
            Mock<IVideoDownloadService> service = BuildService(state);
            using LocalDownloadProgressSource source = BuildSource(service.Object);
            var received = new List<DownloadProgressSnapshot>();
            using IDisposable subscription = source.Progress.Subscribe(s => received.Add(s));
            source.SampleOnce();
            state.PendingActivity.Add("line");

            source.SampleOnce();
            source.SampleOnce();

            service.Verify(s => s.DrainActivityLog(), Times.Exactly(3));
            Assert.Equal(2, received.Count);
            Assert.Equal(new[] { "line" }, received[1].Activity);
        }

        [Fact]
        public void LocalDownloadProgressSource_Progress_NewSubscriberReceivesLatestSnapshot()
        {
            var state = new SampleState();
            using LocalDownloadProgressSource source = BuildSource(BuildService(state).Object);
            source.SampleOnce();

            var received = new List<DownloadProgressSnapshot>();
            using IDisposable subscription = source.Progress.Subscribe(s => received.Add(s));

            Assert.Single(received);
            Assert.Equal(1, received[0].FilesCompleted);
        }

        [Fact]
        public void LocalDownloadProgressSource_SampleOnce_ServiceThrows_EmitsNothingAndDoesNotThrow()
        {
            var service = new Mock<IVideoDownloadService>();
            service.Setup(s => s.GetProgress()).Throws(new InvalidOperationException("provider down"));
            using LocalDownloadProgressSource source = BuildSource(service.Object);
            var received = new List<DownloadProgressSnapshot>();
            using IDisposable subscription = source.Progress.Subscribe(s => received.Add(s));

            source.SampleOnce();

            Assert.Empty(received);
        }

        [Fact]
        public void LocalDownloadProgressSource_Dispose_CompletesSubscribers()
        {
            var source = BuildSource(BuildService(new SampleState()).Object);
            bool completed = false;
            using IDisposable subscription = source.Progress.Subscribe(_ => { }, () => completed = true);

            source.Dispose();

            Assert.True(completed);
        }
    }
}
