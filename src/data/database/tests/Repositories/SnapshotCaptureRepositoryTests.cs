using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    public class SnapshotCaptureRepositoryTests : IAsyncLifetime
    {
        private static readonly DateTimeOffset T0 = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

        private SqliteInMemoryFixture _fixture = null!;
        private SnapshotCaptureRepository _repository = null!;
        private DeviceHealthRepository _healthRepository = null!;
        private LocationRepository _locationRepository = null!;
        private DeviceRepository _deviceRepository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            _repository = new SnapshotCaptureRepository(_fixture.Factory, loggerFactory.CreateLogger<SnapshotCaptureRepository>());
            _healthRepository = new DeviceHealthRepository(_fixture.Factory, loggerFactory.CreateLogger<DeviceHealthRepository>());
            _locationRepository = new LocationRepository(_fixture.Factory, loggerFactory.CreateLogger<LocationRepository>());
            _deviceRepository = new DeviceRepository(_fixture.Factory, loggerFactory.CreateLogger<DeviceRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        /// <summary>Seeds a Location and Device (FK target) and returns the device's internal GUID.</summary>
        private async Task<Guid> SeedDeviceAsync()
        {
            Location location = TestDataBuilder.BuildLocation(name: "Snapshot Site");
            await _locationRepository.AddAsync(location, CancellationToken.None);
            Device device = TestDataBuilder.BuildDevice(locationId: location.Id, name: "Snapshot Camera");
            await _deviceRepository.AddAsync(device, CancellationToken.None);
            return device.Id;
        }

        [Fact]
        public async Task SnapshotCapture_Persist_RoundTripsAllColumns()
        {
            Guid deviceId = await SeedDeviceAsync();
            DeviceHealth health = TestDataBuilder.BuildDeviceHealth(deviceId);
            _ = await _healthRepository.AddAsync(health, CancellationToken.None);

            var capture = SnapshotCapture.Create(deviceId, T0, "fresh", 200, "Devices/x/frame.jpg", health.Id);
            _ = await _repository.AddAsync(capture, CancellationToken.None);

            IReadOnlyList<SnapshotCapture> results = await _repository.GetCurrentForDeviceAsync(
                deviceId, T0.AddMinutes(-1), T0.AddMinutes(1), CancellationToken.None);

            SnapshotCapture persisted = Assert.Single(results);
            Assert.Equal(capture.Id, persisted.Id);
            Assert.Equal(deviceId, persisted.DeviceId);
            Assert.Equal(T0, persisted.CapturedAtUtc);
            Assert.Equal("fresh", persisted.Trigger);
            Assert.Equal(200, persisted.HttpStatus);
            Assert.True(persisted.IsCurrent);
            Assert.Equal("Devices/x/frame.jpg", persisted.MediaFilePath);
            Assert.Equal(health.Id, persisted.DeviceHealthId);
        }

        [Fact]
        public async Task SnapshotCapture_GetCurrentForDevice_ExcludesNonCurrentRows()
        {
            Guid deviceId = await SeedDeviceAsync();

            // Insert out of time order, with a 404 in the middle, to prove the query orders by time.
            _ = await _repository.AddAsync(SnapshotCapture.Create(deviceId, T0.AddMinutes(10), "fresh", 200, "Devices/x/b.jpg", null), CancellationToken.None);
            _ = await _repository.AddAsync(SnapshotCapture.Create(deviceId, T0.AddMinutes(5), "fresh", 404, null, null), CancellationToken.None);
            _ = await _repository.AddAsync(SnapshotCapture.Create(deviceId, T0, "latest", 200, "Devices/x/a.jpg", null), CancellationToken.None);

            IReadOnlyList<SnapshotCapture> results = await _repository.GetCurrentForDeviceAsync(
                deviceId, T0.AddMinutes(-1), T0.AddMinutes(20), CancellationToken.None);

            Assert.Equal(2, results.Count);
            Assert.All(results, r => Assert.True(r.IsCurrent));
            Assert.Equal(new[] { T0, T0.AddMinutes(10) }, results.Select(r => r.CapturedAtUtc).ToArray());
        }
        [Fact]
        public async Task SnapshotCapture_GetAttemptsForDevice_IncludesNonCurrentRowsInOrder()
        {
            Guid deviceId = await SeedDeviceAsync();

            // Failed attempts must come back too: the timeline mapper needs them to end a still's hold.
            _ = await _repository.AddAsync(SnapshotCapture.Create(deviceId, T0.AddMinutes(10), "fresh", 200, "Devices/x/b.jpg", null), CancellationToken.None);
            _ = await _repository.AddAsync(SnapshotCapture.Create(deviceId, T0.AddMinutes(5), "fresh", 404, null, null), CancellationToken.None);
            _ = await _repository.AddAsync(SnapshotCapture.Create(deviceId, T0, "latest", 200, "Devices/x/a.jpg", null), CancellationToken.None);
            _ = await _repository.AddAsync(SnapshotCapture.Create(deviceId, T0.AddMinutes(30), "fresh", 200, "Devices/x/late.jpg", null), CancellationToken.None);

            IReadOnlyList<SnapshotCapture> results = await _repository.GetAttemptsForDeviceAsync(
                deviceId, T0.AddMinutes(-1), T0.AddMinutes(20), CancellationToken.None);

            Assert.Equal(new[] { 200, 404, 200 }, results.Select(r => r.HttpStatus).ToArray());
            Assert.Equal(new[] { T0, T0.AddMinutes(5), T0.AddMinutes(10) }, results.Select(r => r.CapturedAtUtc).ToArray());
        }
    }
}
