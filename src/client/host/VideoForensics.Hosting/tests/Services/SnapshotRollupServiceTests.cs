using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting.BackgroundServices;
using VideoForensics.Client.Hosting.Contracts;
using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Core.Providers.Core;
using VideoForensics.Data.Common.Entities;

using Device = VideoForensics.Data.Common.Entities.Device;
using Location = VideoForensics.Data.Common.Entities.Location;

namespace VideoForensics.Client.Hosting.Tests.Services
{
    /// <summary>The roll-up: stills from the window's snapshot Media rows, one MP4 per device, a Media row for each loop, and failures reported.</summary>
    public class SnapshotRollupServiceTests : IDisposable
    {
        private static readonly DateTime From = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime To = new(2026, 10, 20, 12, 15, 0, DateTimeKind.Utc);
        private const string SnapshotMetadata = "{\"eventId\":\"snapshot-1\",\"type\":\"snapshot\"}";

        private readonly string _root = Path.Combine(Path.GetTempPath(), "vf-rollup-tests-" + Guid.NewGuid().ToString("N"));
        private readonly Guid _accountId = Guid.NewGuid();
        private readonly Mock<ILocationRepository> _locations = new();
        private readonly Mock<IDeviceRepository> _devices = new();
        private readonly Mock<IMediaItemRepository> _media = new();
        private readonly Mock<ITimelineVideoBuilder> _builder = new();
        private readonly Mock<IProviderAccountRepository> _accounts = new();
        private readonly Mock<IStorageLocationProvider> _storage = new();
        private readonly Mock<IForensicsConfigurationService> _configuration = new();
        private readonly Mock<IForensicsConfiguration> _config = new();
        private readonly Mock<IEventRepository> _events = new();
        private readonly Location _location = new() { Id = Guid.NewGuid(), ProviderLocationId = "L", Name = "Home" };
        private readonly Device _device;

        private string MediaRoot => Path.Combine(_root, "media");

        public SnapshotRollupServiceTests()
        {
            _ = Directory.CreateDirectory(_root);
            _device = new Device { Id = Guid.NewGuid(), LocationId = _location.Id, ProviderDeviceId = "porch", Name = "Porch", Type = "camera" };
            _ = _locations.Setup(l => l.ListByProviderAccountIdAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync([_location]);
            _ = _devices.Setup(d => d.GetByLocationIdAsync(_location.Id, It.IsAny<CancellationToken>())).ReturnsAsync([_device]);
            _ = _accounts.Setup(a => a.GetAsync(_accountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProviderAccount { Id = _accountId, ProviderName = "Ring", IsActive = true });
            _ = _storage.Setup(s => s.GetDefaultRoot(StorageCategory.Media)).Returns(() => MediaRoot);
            _ = _storage.Setup(s => s.GetEffectiveRoot(StorageCategory.Media, It.IsAny<string?>()))
                .Returns<StorageCategory, string?>((_, configuredOverride) => string.IsNullOrWhiteSpace(configuredOverride) ? MediaRoot : configuredOverride);
            _ = _configuration.Setup(s => s.LoadConfigurationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(_config.Object);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        private SnapshotRollupService Build()
        {
            return new(
            _locations.Object,
            _devices.Object,
            _media.Object,
            _accounts.Object,
            _storage.Object,
            _builder.Object,
            NullLogger<SnapshotRollupService>.Instance,
            _events.Object,
            configurationService: _configuration.Object);
        }

        /// <summary>Resolver that roots stored (relative) paths at the test folder.</summary>
        private IMediaPathResolver RootedPaths()
        {
            var paths = new Mock<IMediaPathResolver>();
            _ = paths.Setup(p => p.ToAbsoluteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string stored, CancellationToken _) => Path.Combine(_root, stored));
            return paths.Object;
        }

        private string WriteJpeg(string name)
        {
            string path = Path.Combine(_root, name);
            File.WriteAllBytes(path, [0xFF, 0xD8, 0xFF, 0xD9]);
            return path;
        }

        /// <summary>The partial file the builder writes before the rename to the final name.</summary>
        private static string BuildingPathOf(string finalPath)
        {
            return Path.Combine(Path.GetDirectoryName(finalPath)!, Path.GetFileNameWithoutExtension(finalPath) + ".building.mp4");
        }
        private static MediaItem Snapshot(string path, DateTime recordedAt, string metadata = SnapshotMetadata)
        {
            return new()
            {
                Id = Guid.NewGuid(),
                DeviceId = Guid.Empty,
                FileName = Path.GetFileName(path),
                FilePath = path,
                MediaFormat = "jpg",
                RecordedAtUtc = recordedAt,
                Sha256Hash = "h",
                MetadataJson = metadata,
            };
        }

        private void SetWindowRows(params MediaItem[] rows)
        {
            _ = _media.Setup(m => m.GetSnapshotsByDeviceAndDateRangeAsync(_device.Id, From, To, It.IsAny<CancellationToken>())).ReturnsAsync(rows);
        }

        private void BuildSucceeds()
        {
            _ = _builder.Setup(b => b.BuildAsync(It.IsAny<TimelineVideoRequest>(), It.IsAny<CancellationToken>()))
                .Callback<TimelineVideoRequest, CancellationToken>((r, _) => File.WriteAllBytes(r.OutputPath, [0]))
                .ReturnsAsync(new TimelineVideoResult(true, "out.mp4", null, 3));
        }

        [Fact]
        public async Task RollUp_SnapshotsInWindow_BuildsLoopHoldingEachFrameUntilTheNext()
        {
            string first = WriteJpeg("a.jpg");
            string second = WriteJpeg("b.jpg");
            // The database returns rows oldest first, so the fake does too.
            SetWindowRows(Snapshot(first, From.AddMinutes(2)), Snapshot(second, From.AddMinutes(10)));
            TimelineVideoRequest? sent = null;
            _ = _builder.Setup(b => b.BuildAsync(It.IsAny<TimelineVideoRequest>(), It.IsAny<CancellationToken>()))
                .Callback<TimelineVideoRequest, CancellationToken>((r, _) => { sent = r; File.WriteAllBytes(r.OutputPath, [0]); })
                .ReturnsAsync(new TimelineVideoResult(true, "out.mp4", null, 3));

            SnapshotRollupResult result = await Build().RollUpAccountAsync(_accountId, From, To, CancellationToken.None);

            Assert.Equal(1, result.DevicesRolledUp);
            Assert.Null(result.ErrorMessage);
            Assert.NotNull(sent);
            Assert.Equal(new[] { first, second }, sent!.Stills.Select(s => s.ImagePath).ToArray());
            Assert.Equal(TimeSpan.FromMinutes(8), sent.Stills[0].Hold);
            Assert.Equal(TimeSpan.FromMinutes(5), sent.Stills[1].Hold);
            Assert.Equal(360, sent.MaxHeight);
        }

        [Fact]
        public async Task RollUp_RegistersMp4AgainstTheDevice()
        {
            SetWindowRows(Snapshot(WriteJpeg("a.jpg"), From.AddMinutes(1)));
            BuildSucceeds();

            SnapshotRollupResult result = await Build().RollUpAccountAsync(_accountId, From, To, CancellationToken.None);

            Assert.Equal(1, result.DevicesRolledUp);
            _media.Verify(m => m.AddAsync(
                It.Is<MediaItem>(i => i.DeviceId == _device.Id && i.MediaFormat == "mp4" && i.RecordedAtUtc == From
                    && i.DurationSeconds == (To - From).TotalSeconds && i.MetadataJson == SnapshotRollupService.SnapshotRollupMarker),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RollUp_ConfiguredMediaRoot_WritesLoopUnderConfiguredRoot()
        {
            // The loop must sit with the snapshot frames, which the pull and capture write under the Storage Settings Media folder.
            string configuredRoot = Path.Combine(_root, "configured");
            _ = _config.Setup(c => c.DownloadLocation).Returns(configuredRoot);
            SetWindowRows(Snapshot(WriteJpeg("a.jpg"), From.AddMinutes(1)));
            TimelineVideoRequest? sent = null;
            _ = _builder.Setup(b => b.BuildAsync(It.IsAny<TimelineVideoRequest>(), It.IsAny<CancellationToken>()))
                .Callback<TimelineVideoRequest, CancellationToken>((r, _) => { sent = r; File.WriteAllBytes(r.OutputPath, [0]); })
                .ReturnsAsync(new TimelineVideoResult(true, "out.mp4", null, 1));
            string expected = Path.Combine(configuredRoot, "ring", "home", "porch", $"Porch_{To:yyyyMMdd_HHmmss}_snapshot-loop.mp4");

            _ = await Build().RollUpAccountAsync(_accountId, From, To, CancellationToken.None);

            Assert.Equal(BuildingPathOf(expected), sent!.OutputPath);
        }

        [Fact]
        public async Task RollUp_WritesLoopBesideEventsInProviderLocationDeviceFolder()
        {
            // Same folder the event pull and snapshot captures use, so the loop sits with the device's other media.
            SetWindowRows(Snapshot(WriteJpeg("a.jpg"), From.AddMinutes(1)));
            TimelineVideoRequest? sent = null;
            _ = _builder.Setup(b => b.BuildAsync(It.IsAny<TimelineVideoRequest>(), It.IsAny<CancellationToken>()))
                .Callback<TimelineVideoRequest, CancellationToken>((r, _) => { sent = r; File.WriteAllBytes(r.OutputPath, [0]); })
                .ReturnsAsync(new TimelineVideoResult(true, "out.mp4", null, 1));
            string expected = Path.Combine(MediaRoot, "ring", "home", "porch", $"Porch_{To:yyyyMMdd_HHmmss}_snapshot-loop.mp4");

            _ = await Build().RollUpAccountAsync(_accountId, From, To, CancellationToken.None);

            Assert.Equal(BuildingPathOf(expected), sent!.OutputPath);
            _media.Verify(m => m.AddAsync(It.Is<MediaItem>(i => i.FilePath == expected), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RollUp_AccountNotFound_ReportsErrorAndBuildsNothing()
        {
            _ = _accounts.Setup(a => a.GetAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync((ProviderAccount?)null);
            SetWindowRows(Snapshot(WriteJpeg("a.jpg"), From.AddMinutes(1)));

            SnapshotRollupResult result = await Build().RollUpAccountAsync(_accountId, From, To, CancellationToken.None);

            Assert.Equal("Account not found", result.ErrorMessage);
            _builder.Verify(b => b.BuildAsync(It.IsAny<TimelineVideoRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RollUp_SkipsSnapshotRowsWhoseFileIsMissing()
        {
            // The database already excludes non-snapshot media; the only check left in the service is the file on disk.
            string real = WriteJpeg("a.jpg");
            SetWindowRows(
                Snapshot(real, From.AddMinutes(1)),
                Snapshot(Path.Combine(_root, "gone.jpg"), From.AddMinutes(3)));
            TimelineVideoRequest? sent = null;
            _ = _builder.Setup(b => b.BuildAsync(It.IsAny<TimelineVideoRequest>(), It.IsAny<CancellationToken>()))
                .Callback<TimelineVideoRequest, CancellationToken>((r, _) => { sent = r; File.WriteAllBytes(r.OutputPath, [0]); })
                .ReturnsAsync(new TimelineVideoResult(true, "out.mp4", null, 1));

            _ = await Build().RollUpAccountAsync(_accountId, From, To, CancellationToken.None);

            Assert.Equal(new[] { real }, sent!.Stills.Select(s => s.ImagePath).ToArray());
        }

        [Fact]
        public async Task RollUp_NoSnapshotsInWindow_IsNotAFailureAndRegistersNothing()
        {
            SetWindowRows();

            SnapshotRollupResult result = await Build().RollUpAccountAsync(_accountId, From, To, CancellationToken.None);

            Assert.Equal(0, result.DevicesRolledUp);
            Assert.Null(result.ErrorMessage);
            _builder.Verify(b => b.BuildAsync(It.IsAny<TimelineVideoRequest>(), It.IsAny<CancellationToken>()), Times.Never);
            _media.Verify(m => m.AddAsync(It.IsAny<MediaItem>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RollUp_WindowWithCapturesButNoUsableStill_LogsWhyNothingWasBuilt()
        {
            // Production symptom: roll-ups record success but write no loop. An empty window must say why, not stay silent.
            var captures = new Mock<ISnapshotCaptureRepository>();
            _ = captures.Setup(c => c.GetAttemptsForDeviceAsync(_device.Id, From, To, It.IsAny<CancellationToken>())).ReturnsAsync(
            [
                SnapshotCapture.Create(_device.Id, From.AddMinutes(1), "fresh", 200, "gone.jpg", null),
            ]);
            var logger = new Mock<ILogger<SnapshotRollupService>>();
            SnapshotRollupService service = new(
                _locations.Object, _devices.Object, _media.Object, _accounts.Object, _storage.Object, _builder.Object,
                logger.Object,
                _events.Object,
                captures: captures.Object,
                stillMapper: new SnapshotStillMapper(),
                mediaPaths: RootedPaths());

            SnapshotRollupResult result = await service.RollUpAccountAsync(_accountId, From, To, CancellationToken.None);

            Assert.Equal(0, result.DevicesRolledUp);
            Assert.Null(result.ErrorMessage);
            Assert.Contains(logger.Invocations, i => i.Method.Name == nameof(ILogger.Log) && Equals(i.Arguments[0], LogLevel.Warning));
        }
        [Fact]
        public async Task RollUp_BuildFails_ReportsFailureWithoutRegisteringLoop()
        {
            SetWindowRows(Snapshot(WriteJpeg("a.jpg"), From.AddMinutes(1)));
            _ = _builder.Setup(b => b.BuildAsync(It.IsAny<TimelineVideoRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TimelineVideoResult(false, null, "ffmpeg not found", 0));

            SnapshotRollupResult result = await Build().RollUpAccountAsync(_accountId, From, To, CancellationToken.None);

            Assert.Equal(0, result.DevicesRolledUp);
            Assert.Contains("ffmpeg not found", result.ErrorMessage);
            _media.Verify(m => m.AddAsync(It.IsAny<MediaItem>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RollUp_FailedCaptureBetweenStills_EndsTheHoldSoTheOutageIsAGap()
        {
            // A 200 at +1 min, a failed attempt at +3 min, then a 200 at +10 min. The first still must end at the failure,
            // not run on to the next image, so the outage is a gap rather than a frame that looks current.
            var captures = new Mock<ISnapshotCaptureRepository>();
            _ = captures.Setup(c => c.GetAttemptsForDeviceAsync(_device.Id, From, To, It.IsAny<CancellationToken>())).ReturnsAsync(
            [
                SnapshotCapture.Create(_device.Id, From.AddMinutes(1), "fresh", 200, "a.jpg", null),
                SnapshotCapture.Create(_device.Id, From.AddMinutes(3), "fresh", 503, null, null),
                SnapshotCapture.Create(_device.Id, From.AddMinutes(10), "fresh", 200, "b.jpg", null),
            ]);
            _ = WriteJpeg("a.jpg");
            _ = WriteJpeg("b.jpg");
            TimelineVideoRequest? sent = null;
            _ = _builder.Setup(b => b.BuildAsync(It.IsAny<TimelineVideoRequest>(), It.IsAny<CancellationToken>()))
                .Callback<TimelineVideoRequest, CancellationToken>((r, _) => { sent = r; File.WriteAllBytes(r.OutputPath, [0]); })
                .ReturnsAsync(new TimelineVideoResult(true, "out.mp4", null, 3));

            SnapshotRollupService service = new(
                _locations.Object, _devices.Object, _media.Object, _accounts.Object, _storage.Object, _builder.Object,
                NullLogger<SnapshotRollupService>.Instance,
                _events.Object,
                captures: captures.Object,
                stillMapper: new SnapshotStillMapper(),
                mediaPaths: RootedPaths());

            SnapshotRollupResult result = await service.RollUpAccountAsync(_accountId, From, To, CancellationToken.None);

            Assert.Equal(1, result.DevicesRolledUp);
            Assert.NotNull(sent);
            Assert.Equal(TimeSpan.FromMinutes(2), sent!.Stills[0].Hold);
            Assert.Equal(TimeSpan.FromMinutes(5), sent.Stills[1].Hold);
        }

        [Fact]
        public async Task RollUp_RecordsTimelapseEventForTheDevice_AlreadyDownloaded()
        {
            // The Events tab reads the Events table, so each loop also needs an event row. It is marked Downloaded so the
            // event media puller does not try to fetch it from the provider.
            SetWindowRows(Snapshot(WriteJpeg("a.jpg"), From.AddMinutes(1)));
            BuildSucceeds();

            _ = await Build().RollUpAccountAsync(_accountId, From, To, CancellationToken.None);

            _events.Verify(e => e.UpsertAsync(
                It.Is<Event>(ev => ev.DeviceId == _device.Id
                    && ev.EventType == "timelapse"
                    && ev.OccurredAtUtc == From
                    && ev.DownloadStatus == EventDownloadStatus.Downloaded
                    && ev.DownloadedAtUtc.HasValue
                    && ev.ProviderEventId == $"timelapse-{To:yyyyMMddHHmmss}"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RollUp_SuccessfulLoop_LogsInformationWithDeviceAndFrameCount()
        {
            // A successful loop must leave a trace: the device, how many snapshots went in, and the file name.
            SetWindowRows(Snapshot(WriteJpeg("a.jpg"), From.AddMinutes(1)), Snapshot(WriteJpeg("b.jpg"), From.AddMinutes(5)));
            BuildSucceeds();
            var logger = new Mock<ILogger<SnapshotRollupService>>();
            SnapshotRollupService service = new(
                _locations.Object, _devices.Object, _media.Object, _accounts.Object, _storage.Object, _builder.Object,
                logger.Object,
                _events.Object,
                configurationService: _configuration.Object);

            _ = await service.RollUpAccountAsync(_accountId, From, To, CancellationToken.None);

            Assert.Contains(logger.Invocations, i => i.Method.Name == nameof(ILogger.Log)
                && Equals(i.Arguments[0], LogLevel.Information)
                && i.Arguments[2].ToString()!.Contains("Porch", StringComparison.Ordinal)
                && i.Arguments[2].ToString()!.Contains("2 snapshots", StringComparison.Ordinal));
        }

        [Fact]
        public async Task RollUpSegment_WritesToBuildingFileAndRegistersLoopWithTheSpan()
        {
            SetWindowRows(Snapshot(WriteJpeg("a.jpg"), From.AddMinutes(1)));
            TimelineVideoRequest? sent = null;
            _ = _builder.Setup(b => b.BuildAsync(It.IsAny<TimelineVideoRequest>(), It.IsAny<CancellationToken>()))
                .Callback<TimelineVideoRequest, CancellationToken>((r, _) => { sent = r; File.WriteAllBytes(r.OutputPath, [0]); })
                .ReturnsAsync(new TimelineVideoResult(true, "out.mp4", null, 3));

            SnapshotRollupResult result = await Build().RollUpSegmentAsync(_accountId, _device.Id, From, To, CancellationToken.None);

            Assert.Equal(1, result.DevicesRolledUp);
            Assert.Null(result.ErrorMessage);
            Assert.NotNull(sent);
            Assert.Contains(".building.mp4", sent!.OutputPath, StringComparison.Ordinal);
            Assert.Empty(Directory.GetFiles(_root, "*.building.mp4", SearchOption.AllDirectories));
            _media.Verify(m => m.AddAsync(It.Is<MediaItem>(i => i.RecordedAtUtc == From
                && i.DurationSeconds == (To - From).TotalSeconds
                && i.MetadataJson == SnapshotRollupService.SnapshotRollupMarker), It.IsAny<CancellationToken>()), Times.Once);
        }    }
}
