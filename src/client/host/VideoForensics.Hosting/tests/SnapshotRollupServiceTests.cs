using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Services;
using VideoForensics.Hosting.BackgroundServices;
using VideoForensics.Hosting.Contracts;
using VideoForensics.Hosting.Services;
using Xunit;
using Device = VideoForensics.Data.Common.Entities.Device;
using Location = VideoForensics.Data.Common.Entities.Location;

namespace VideoForensics.Hosting.Tests.Services
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
        private readonly Location _location = new() { Id = Guid.NewGuid(), ProviderLocationId = "L", Name = "Home" };
        private readonly Device _device;

        public SnapshotRollupServiceTests()
        {
            _ = Directory.CreateDirectory(_root);
            _device = new Device { Id = Guid.NewGuid(), LocationId = _location.Id, ProviderDeviceId = "porch", Name = "Porch", Type = "camera" };
            _locations.Setup(l => l.ListByProviderAccountIdAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Location> { _location });
            _devices.Setup(d => d.GetByLocationIdAsync(_location.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Device> { _device });
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        private SnapshotRollupService Build() => new(
            _locations.Object,
            _devices.Object,
            _media.Object,
            _builder.Object,
            Options.Create(new SnapshotTimelineOptions { SnapshotRoot = _root }),
            NullLogger<SnapshotRollupService>.Instance);

        private string WriteJpeg(string name)
        {
            string path = Path.Combine(_root, name);
            File.WriteAllBytes(path, [0xFF, 0xD8, 0xFF, 0xD9]);
            return path;
        }

        private static MediaItem Snapshot(string path, DateTime recordedAt, string metadata = SnapshotMetadata) => new()
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

        private void SetWindowRows(params MediaItem[] rows) =>
            _media.Setup(m => m.GetByDeviceAndDateRangeAsync(_device.Id, From, To, It.IsAny<CancellationToken>())).ReturnsAsync(rows);

        private void BuildSucceeds() =>
            _builder.Setup(b => b.BuildAsync(It.IsAny<TimelineVideoRequest>(), It.IsAny<CancellationToken>()))
                .Callback<TimelineVideoRequest, CancellationToken>((r, _) => File.WriteAllBytes(r.OutputPath, [0]))
                .ReturnsAsync(new TimelineVideoResult(true, "out.mp4", null, 3));
        [Fact]
        public async Task RollUp_SnapshotsInWindow_BuildsLoopHoldingEachFrameUntilTheNext()
        {
            string first = WriteJpeg("a.jpg");
            string second = WriteJpeg("b.jpg");
            SetWindowRows(Snapshot(second, From.AddMinutes(10)), Snapshot(first, From.AddMinutes(2)));
            TimelineVideoRequest? sent = null;
            _builder.Setup(b => b.BuildAsync(It.IsAny<TimelineVideoRequest>(), It.IsAny<CancellationToken>()))
                .Callback<TimelineVideoRequest, CancellationToken>((r, _) => { sent = r; File.WriteAllBytes(r.OutputPath, [0]); })
                .ReturnsAsync(new TimelineVideoResult(true, "out.mp4", null, 3));

            SnapshotRollupResult result = await Build().RollUpAccountAsync(_accountId, From, To, CancellationToken.None);

            Assert.Equal(1, result.DevicesRolledUp);
            Assert.Null(result.ErrorMessage);
            Assert.NotNull(sent);
            Assert.Equal(new[] { first, second }, sent!.Stills.Select(s => s.ImagePath).ToArray());
            Assert.Equal(TimeSpan.FromMinutes(8), sent.Stills[0].Hold);
            Assert.Equal(TimeSpan.FromMinutes(5), sent.Stills[1].Hold);
        }

        [Fact]
        public async Task RollUp_RegistersMp4AgainstTheDevice()
        {
            SetWindowRows(Snapshot(WriteJpeg("a.jpg"), From.AddMinutes(1)));
            BuildSucceeds();

            SnapshotRollupResult result = await Build().RollUpAccountAsync(_accountId, From, To, CancellationToken.None);

            Assert.Equal(1, result.DevicesRolledUp);
            _media.Verify(m => m.AddAsync(
                It.Is<MediaItem>(i => i.DeviceId == _device.Id && i.MediaFormat == "mp4" && i.RecordedAtUtc == To),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RollUp_IgnoresNonSnapshotMediaAndMissingFiles()
        {
            string real = WriteJpeg("a.jpg");
            string other = WriteJpeg("clip.jpg");
            SetWindowRows(
                Snapshot(real, From.AddMinutes(1)),
                Snapshot(other, From.AddMinutes(2), metadata: "{\"type\":\"event\"}"),
                Snapshot(Path.Combine(_root, "gone.jpg"), From.AddMinutes(3)));
            TimelineVideoRequest? sent = null;
            _builder.Setup(b => b.BuildAsync(It.IsAny<TimelineVideoRequest>(), It.IsAny<CancellationToken>()))
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
            _builder.Setup(b => b.BuildAsync(It.IsAny<TimelineVideoRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TimelineVideoResult(false, null, "ffmpeg not found", 0));

            SnapshotRollupResult result = await Build().RollUpAccountAsync(_accountId, From, To, CancellationToken.None);

            Assert.Equal(0, result.DevicesRolledUp);
            Assert.Contains("ffmpeg not found", result.ErrorMessage);
            _media.Verify(m => m.AddAsync(It.IsAny<MediaItem>(), It.IsAny<CancellationToken>()), Times.Never);
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
            SyncSchedule schedule = SyncSchedule.CreateDefault(Guid.NewGuid());
            schedule.LastRollupUtc = Now.AddMinutes(-14);

            Assert.False(ScheduleCalculator.IsRollupDue(schedule, Now));
        }

        [Fact]
        public void AtInterval_IsDue()
        {
            SyncSchedule schedule = SyncSchedule.CreateDefault(Guid.NewGuid());
            schedule.LastRollupUtc = Now.AddMinutes(-15);

            Assert.True(ScheduleCalculator.IsRollupDue(schedule, Now));
        }
    }
}
