using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Core.Providers.Core;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Services;
using VideoForensics.Client.Hosting.BackgroundServices;
using VideoForensics.Client.Hosting.Contracts;

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
            Options.Create(new SnapshotTimelineOptions { SnapshotRoot = _root }),
            NullLogger<SnapshotRollupService>.Instance);
        }

        private string WriteJpeg(string name)
        {
            string path = Path.Combine(_root, name);
            File.WriteAllBytes(path, [0xFF, 0xD8, 0xFF, 0xD9]);
            return path;
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
                It.Is<MediaItem>(i => i.DeviceId == _device.Id && i.MediaFormat == "mp4" && i.RecordedAtUtc == To && i.MetadataJson == SnapshotRollupService.SnapshotRollupMarker),
                It.IsAny<CancellationToken>()), Times.Once);
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
            string expected = Path.Combine(MediaRoot, "ring", "home", "porch", $"{To:yyyyMMddHHmmss}.mp4");

            _ = await Build().RollUpAccountAsync(_accountId, From, To, CancellationToken.None);

            Assert.Equal(expected, sent!.OutputPath);
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
                Options.Create(new SnapshotTimelineOptions { SnapshotRoot = _root }),
                NullLogger<SnapshotRollupService>.Instance,
                captures: captures.Object,
                stillMapper: new SnapshotStillMapper());

            SnapshotRollupResult result = await service.RollUpAccountAsync(_accountId, From, To, CancellationToken.None);

            Assert.Equal(1, result.DevicesRolledUp);
            Assert.NotNull(sent);
            Assert.Equal(TimeSpan.FromMinutes(2), sent!.Stills[0].Hold);
            Assert.Equal(TimeSpan.FromMinutes(5), sent.Stills[1].Hold);
        }
    }

    /// <summary>When a roll-up is due: never rolled up, or the interval has passed since the last one.</summary>
    public class ScheduleRollupDueTests
    {
        private static readonly DateTime Now = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void NeverRolledUp_IsDue()
        {
            Assert.True(ScheduleCalculator.IsRollupDue(SyncSchedule.CreateDefault(Guid.NewGuid()), Now));
        }

        [Fact]
        public void WithinInterval_IsNotDue()
        {
            var schedule = SyncSchedule.CreateDefault(Guid.NewGuid());
            schedule.LastRollupUtc = Now.AddMinutes(-14);

            Assert.False(ScheduleCalculator.IsRollupDue(schedule, Now));
        }

        [Fact]
        public void AtInterval_IsDue()
        {
            var schedule = SyncSchedule.CreateDefault(Guid.NewGuid());
            schedule.LastRollupUtc = Now.AddMinutes(-15);

            Assert.True(ScheduleCalculator.IsRollupDue(schedule, Now));
        }
    }
}
