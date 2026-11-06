using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Moq;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Core.Providers.Core;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Services;

using Xunit;

namespace VideoForensics.Data.Core.Tests
{
    /// <summary>Device timelines come from stored snapshot attempts only, mapped by the real still mapper into a mocked builder.</summary>
    public class SnapshotTimelineServiceTests : IDisposable
    {
        private static readonly DateTimeOffset T0 = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
        private static readonly DateTimeOffset From = T0.AddHours(-1);
        private static readonly DateTimeOffset To = T0.AddHours(1);

        private readonly string _root = Path.Combine(Path.GetTempPath(), "vf-timeline-tests-" + Guid.NewGuid().ToString("N"));
        private readonly string _output = Path.Combine(Path.GetTempPath(), "vf-timeline-tests-out.mp4");
        private readonly Mock<ISnapshotCaptureRepository> _captures = new();
        private readonly Mock<ITimelineVideoBuilder> _builder = new();
        private readonly Guid _deviceId = Guid.NewGuid();

        public SnapshotTimelineServiceTests()
        {
            _ = Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        /// <summary>Resolver that roots stored (relative) paths at the test folder.</summary>
        private IMediaPathResolver RootedPaths()
        {
            var paths = new Mock<IMediaPathResolver>();
            _ = paths.Setup(p => p.ToAbsoluteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string stored, CancellationToken _) => Path.Combine(_root, stored));
            return paths.Object;
        }

        private SnapshotTimelineService CreateService()
        {
            return new(_captures.Object, new SnapshotStillMapper(), _builder.Object, NullLogger<SnapshotTimelineService>.Instance, RootedPaths());
        }

        private string WriteMedia(string relativePath)
        {
            string full = Path.Combine(_root, relativePath);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllBytes(full, [0]);
            return full;
        }

        private SnapshotCapture Attempt(DateTimeOffset at, int status, string? media)
        {
            return SnapshotCapture.Create(_deviceId, at, "fresh", status, media, null);
        }

        private void SetupAttempts(params SnapshotCapture[] rows)
        {
            _ = _captures.Setup(x => x.GetAttemptsForDeviceAsync(_deviceId, From, To, It.IsAny<CancellationToken>())).ReturnsAsync(rows);
        }

        private void SetupBuilderSuccess()
        {
            _ = _builder.Setup(x => x.BuildAsync(It.IsAny<TimelineVideoRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((TimelineVideoRequest r, CancellationToken _) => new TimelineVideoResult(true, r.OutputPath, null, 0));
        }

        [Fact]
        public async Task SnapshotTimelineService_StoredRows_BecomeStillsHeldUntilNextCapture()
        {
            string first = WriteMedia("dev/a.jpg");
            string second = WriteMedia("dev/b.jpg");
            SetupAttempts(
                Attempt(T0, 200, "dev/a.jpg"),
                Attempt(T0.AddSeconds(10), 200, "dev/b.jpg"));
            SetupBuilderSuccess();
            using var cts = new CancellationTokenSource();

            string? result = await CreateService().BuildDeviceTimelineAsync(_deviceId, From, To, _output, cts.Token);

            Assert.Equal(_output, result);
            _captures.Verify(x => x.GetAttemptsForDeviceAsync(_deviceId, From, To, cts.Token), Times.Once);
            _builder.Verify(x => x.BuildAsync(It.IsAny<TimelineVideoRequest>(), cts.Token), Times.Once);
            TimelineVideoRequest request = Assert.Single(_builder.Invocations).Arguments[0] as TimelineVideoRequest
                ?? throw new InvalidOperationException("Builder was not called with a request.");
            Assert.Equal(_output, request.OutputPath);
            Assert.Null(request.Clips);
            Assert.Equal(2, request.Stills.Count);
            Assert.Equal(Path.GetFullPath(first), request.Stills[0].ImagePath);
            Assert.Equal(T0, request.Stills[0].CapturedAt);
            Assert.Equal(TimeSpan.FromSeconds(10), request.Stills[0].Hold);
            Assert.Equal(Path.GetFullPath(second), request.Stills[1].ImagePath);
            Assert.Equal(T0.AddSeconds(10), request.Stills[1].CapturedAt);
            Assert.Equal(TimeSpan.FromSeconds(1), request.Stills[1].Hold);
        }

        [Fact]
        public async Task SnapshotTimelineService_NoStoredStills_ReturnsNullAndSkipsBuilder()
        {
            SetupAttempts();

            string? result = await CreateService().BuildDeviceTimelineAsync(_deviceId, From, To, _output, CancellationToken.None);

            Assert.Null(result);
            _builder.Verify(x => x.BuildAsync(It.IsAny<TimelineVideoRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SnapshotTimelineService_Status200WithMissingFile_ProducesNoStill()
        {
            _ = WriteMedia("dev/present.jpg");
            SetupAttempts(
                Attempt(T0, 200, "dev/present.jpg"),
                Attempt(T0.AddSeconds(5), 200, "dev/deleted.jpg"));
            SetupBuilderSuccess();

            string? result = await CreateService().BuildDeviceTimelineAsync(_deviceId, From, To, _output, CancellationToken.None);

            Assert.Equal(_output, result);
            var request = (TimelineVideoRequest)Assert.Single(_builder.Invocations).Arguments[0];
            TimelineStill only = Assert.Single(request.Stills);
            Assert.Equal(Path.GetFullPath(Path.Combine(_root, "dev/present.jpg")), only.ImagePath);
            Assert.Equal(TimeSpan.FromSeconds(1), only.Hold);
        }

        [Fact]
        public async Task SnapshotTimelineService_OnlyMissingFiles_ReturnsNullAndSkipsBuilder()
        {
            SetupAttempts(Attempt(T0, 200, "dev/gone.jpg"));

            string? result = await CreateService().BuildDeviceTimelineAsync(_deviceId, From, To, _output, CancellationToken.None);

            Assert.Null(result);
            _builder.Verify(x => x.BuildAsync(It.IsAny<TimelineVideoRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SnapshotTimelineService_BuilderFails_ReturnsNullInsteadOfOutputPath()
        {
            _ = WriteMedia("dev/a.jpg");
            SetupAttempts(Attempt(T0, 200, "dev/a.jpg"));
            _ = _builder.Setup(x => x.BuildAsync(It.IsAny<TimelineVideoRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TimelineVideoResult(false, null, "ffmpeg failed", 0));

            string? result = await CreateService().BuildDeviceTimelineAsync(_deviceId, From, To, _output, CancellationToken.None);

            Assert.Null(result);
        }
    }
}
