using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Data.Core.Services;
using VideoForensics.Hosting.BackgroundServices;
using VideoForensics.Hosting.Contracts;
using VideoForensics.Hosting.Services;
using Xunit;

namespace VideoForensics.Hosting.Tests.Services
{
    /// <summary>The roll-up: one MP4 loop per device with stills, a Media row for each loop, and failures reported.</summary>
    public class SnapshotRollupServiceTests : IDisposable
    {
        private static readonly DateTime From = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime To = new(2026, 10, 20, 12, 15, 0, DateTimeKind.Utc);

        private readonly string _root = Path.Combine(Path.GetTempPath(), "vf-rollup-tests-" + Guid.NewGuid().ToString("N"));
        private readonly Guid _accountId = Guid.NewGuid();
        private readonly Mock<ILocationRepository> _locations = new();
        private readonly Mock<IDeviceRepository> _devices = new();
        private readonly Mock<ISnapshotTimelineService> _timeline = new();
        private readonly Mock<IMediaItemRepository> _media = new();
        private readonly Location _location = new() { Id = Guid.NewGuid(), ProviderLocationId = "L", Name = "Home" };

        public SnapshotRollupServiceTests()
        {
            _ = Directory.CreateDirectory(_root);
            _locations.Setup(l => l.ListByProviderAccountIdAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Location> { _location });
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
            _timeline.Object,
            _media.Object,
            Options.Create(new SnapshotTimelineOptions { SnapshotRoot = _root }),
            NullLogger<SnapshotRollupService>.Instance);

        private DeviceEntity AddDevice(string name)
        {
            var device = new DeviceEntity { Id = Guid.NewGuid(), LocationId = _location.Id, ProviderDeviceId = name, Name = name, Type = "camera" };
            _devices.Setup(d => d.GetByLocationIdAsync(_location.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new List<DeviceEntity> { device });
            return device;
        }

        private string FakeMp4(string name)
        {
            string path = Path.Combine(_root, name);
            File.WriteAllBytes(path, [1, 2, 3, 4]);
            return path;
        }

        [Fact]
        public async Task RollUp_DeviceWithStills_RegistersMp4AndReportsRolledUp()
        {
            DeviceEntity device = AddDevice("Porch");
            string mp4 = FakeMp4("loop.mp4");
            _timeline.Setup(t => t.BuildDeviceTimelineAsync(device.Id, It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mp4);

            SnapshotRollupResult result = await Build().RollUpAccountAsync(_accountId, From, To, CancellationToken.None);

            Assert.Equal(1, result.DevicesRolledUp);
            Assert.Null(result.ErrorMessage);
            _media.Verify(m => m.AddAsync(
                It.Is<MediaItem>(i => i.DeviceId == device.Id && i.MediaFormat == "mp4" && i.FilePath == mp4 && i.RecordedAtUtc == To),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RollUp_DeviceWithNoStills_IsNotAFailureAndRegistersNothing()
        {
            DeviceEntity device = AddDevice("Porch");
            _timeline.Setup(t => t.BuildDeviceTimelineAsync(device.Id, It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);

            SnapshotRollupResult result = await Build().RollUpAccountAsync(_accountId, From, To, CancellationToken.None);

            Assert.Equal(0, result.DevicesRolledUp);
            Assert.Null(result.ErrorMessage);
            _media.Verify(m => m.AddAsync(It.IsAny<MediaItem>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RollUp_DeviceThrows_ReportsFailureWithoutRegisteringIt()
        {
            DeviceEntity device = AddDevice("Porch");
            _timeline.Setup(t => t.BuildDeviceTimelineAsync(device.Id, It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("ffmpeg not found"));

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
