using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;
using VideoForensics.Utils.Testing;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    /// <summary>The placeholder flag on a MediaItem and the skip reason on a SnapshotCapture are stored and read back.</summary>
    public class SnapshotPlaceholderRepositoryTests : IAsyncLifetime
    {
        private static readonly DateTimeOffset T0 = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

        private SqliteInMemoryFixture _fixture = null!;
        private SnapshotCaptureRepository _captures = null!;
        private MediaItemRepository _media = null!;
        private LocationRepository _locations = null!;
        private DeviceRepository _devices = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            _captures = new SnapshotCaptureRepository(NewUnitOfWork(loggerFactory), loggerFactory.CreateLogger<SnapshotCaptureRepository>());
            _media = new MediaItemRepository(NewUnitOfWork(loggerFactory), NullLogger<MediaItemRepository>.Instance);
            _locations = new LocationRepository(NewUnitOfWork(loggerFactory), loggerFactory.CreateLogger<LocationRepository>());
            _devices = new DeviceRepository(NewUnitOfWork(loggerFactory), loggerFactory.CreateLogger<DeviceRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        private UnitOfWork NewUnitOfWork(ILoggerFactory loggerFactory) =>
            new(_fixture.Factory, new TestServiceProvider(_fixture, loggerFactory), loggerFactory.CreateLogger<UnitOfWork>());

        /// <summary>Seeds a Location and Device (FK target) and returns the device's internal GUID.</summary>
        private async Task<Guid> SeedDeviceAsync()
        {
            Location location = TestDataBuilder.BuildLocation(name: "Placeholder Site");
            await _locations.AddAsync(location, CancellationToken.None);
            Device device = TestDataBuilder.BuildDevice(locationId: location.Id, name: "Placeholder Camera");
            await _devices.AddAsync(device, CancellationToken.None);
            return device.Id;
        }

        [Fact]
        public async Task MediaItem_IsPlaceholderTrue_ReadsBackTrue()
        {
            Guid deviceId = await SeedDeviceAsync();
            var item = new MediaItem
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                FileName = "gray.jpg",
                FilePath = "Devices/x/gray.jpg",
                MediaFormat = "jpeg",
                FileSizeBytes = 5442,
                RecordedAtUtc = T0.UtcDateTime,
                DownloadedAtUtc = T0.UtcDateTime,
                Sha256Hash = "2653d118053d",
                IsPlaceholder = true,
            };
            await _media.AddAsync(item, CancellationToken.None);

            MediaItem? read = await _media.GetAsync(item.Id, CancellationToken.None);

            Assert.NotNull(read);
            Assert.True(read!.IsPlaceholder);
        }

        [Fact]
        public async Task SnapshotCapture_SkipReasonIdenticalFrame_ReadsBackWithNoMediaPath()
        {
            Guid deviceId = await SeedDeviceAsync();
            var skipped = SnapshotCapture.Create(deviceId, T0, "fresh", 200, null, null, SnapshotSkipReason.IdenticalFrame);
            _ = await _captures.AddAsync(skipped, CancellationToken.None);

            IReadOnlyList<SnapshotCapture> rows = await _captures.GetAttemptsForDeviceAsync(
                deviceId, T0.AddMinutes(-1), T0.AddMinutes(1), CancellationToken.None);

            SnapshotCapture row = Assert.Single(rows);
            Assert.Equal(SnapshotSkipReason.IdenticalFrame, row.SkipReason);
            Assert.Equal(200, row.HttpStatus);
            Assert.Null(row.MediaFilePath);
        }
    }
}
