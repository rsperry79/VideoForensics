using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    public class DeviceHealthRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private DeviceHealthRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            _repository = new DeviceHealthRepository(_fixture.Factory, loggerFactory.CreateLogger<DeviceHealthRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task DeviceHealthRepository_AddAsync_RecordsHealthMetric()
        {
            var deviceId = Guid.NewGuid();
            DeviceHealth health = TestDataBuilder.BuildDeviceHealth(deviceId);

            DeviceHealth result = await _repository.AddAsync(health, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(health.Id, result.Id);
            Assert.Equal(deviceId, result.DeviceId);
            Assert.Equal(80m, result.BatteryPercentage);
            Assert.Equal("1.0.0", result.FirmwareVersion);
        }

        [Fact]
        public async Task DeviceHealthRepository_GetLatestAsync_ReturnsNewestRecord()
        {
            var deviceId = Guid.NewGuid();
            DateTime now = DateTime.UtcNow;

            // Add first health record
            DeviceHealth health1 = TestDataBuilder.BuildDeviceHealth(deviceId);
            health1.CapturedAtUtc = now.AddMinutes(-5);
            health1.BatteryPercentage = 70m;
            _ = await _repository.AddAsync(health1, CancellationToken.None);

            // Add second (newer) health record
            DeviceHealth health2 = TestDataBuilder.BuildDeviceHealth(deviceId);
            health2.CapturedAtUtc = now;
            health2.BatteryPercentage = 85m;
            _ = await _repository.AddAsync(health2, CancellationToken.None);

            DeviceHealth? latest = await _repository.GetLatestAsync(deviceId, CancellationToken.None);

            Assert.NotNull(latest);
            Assert.Equal(health2.Id, latest.Id);
            Assert.Equal(85m, latest.BatteryPercentage);
        }

        [Fact]
        public async Task DeviceHealthRepository_GetLatestAsync_ReturnsNullWhenDeviceNotFound()
        {
            var nonExistentDeviceId = Guid.NewGuid();

            DeviceHealth? result = await _repository.GetLatestAsync(nonExistentDeviceId, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task DeviceHealthRepository_GetHistoryAsync_ReturnsAllRecordsNewestFirst()
        {
            var deviceId = Guid.NewGuid();
            DateTime now = DateTime.UtcNow;

            // Add records in non-chronological order
            DeviceHealth health1 = TestDataBuilder.BuildDeviceHealth(deviceId);
            health1.CapturedAtUtc = now.AddMinutes(-10);
            health1.BatteryPercentage = 60m;
            _ = await _repository.AddAsync(health1, CancellationToken.None);

            DeviceHealth health2 = TestDataBuilder.BuildDeviceHealth(deviceId);
            health2.CapturedAtUtc = now;
            health2.BatteryPercentage = 85m;
            _ = await _repository.AddAsync(health2, CancellationToken.None);

            DeviceHealth health3 = TestDataBuilder.BuildDeviceHealth(deviceId);
            health3.CapturedAtUtc = now.AddMinutes(-5);
            health3.BatteryPercentage = 75m;
            _ = await _repository.AddAsync(health3, CancellationToken.None);

            IReadOnlyList<DeviceHealth> history = await _repository.GetHistoryAsync(deviceId, CancellationToken.None);

            Assert.Equal(3, history.Count);
            // Verify they come back newest first
            Assert.Equal(health2.Id, history[0].Id);
            Assert.Equal(85m, history[0].BatteryPercentage);
            Assert.Equal(health3.Id, history[1].Id);
            Assert.Equal(75m, history[1].BatteryPercentage);
            Assert.Equal(health1.Id, history[2].Id);
            Assert.Equal(60m, history[2].BatteryPercentage);
        }

        [Fact]
        public async Task DeviceHealthRepository_GetHistoryAsync_ReturnsEmptyListWhenDeviceNotFound()
        {
            var nonExistentDeviceId = Guid.NewGuid();

            IReadOnlyList<DeviceHealth> history = await _repository.GetHistoryAsync(nonExistentDeviceId, CancellationToken.None);

            Assert.Empty(history);
        }

        [Fact]
        public async Task DeviceHealthRepository_GetHistoryAsync_FiltersOnlyByDeviceId()
        {
            var deviceId1 = Guid.NewGuid();
            var deviceId2 = Guid.NewGuid();

            DeviceHealth health1 = TestDataBuilder.BuildDeviceHealth(deviceId1);
            _ = await _repository.AddAsync(health1, CancellationToken.None);

            DeviceHealth health2 = TestDataBuilder.BuildDeviceHealth(deviceId2);
            _ = await _repository.AddAsync(health2, CancellationToken.None);

            DeviceHealth health3 = TestDataBuilder.BuildDeviceHealth(deviceId1);
            _ = await _repository.AddAsync(health3, CancellationToken.None);

            IReadOnlyList<DeviceHealth> history = await _repository.GetHistoryAsync(deviceId1, CancellationToken.None);

            Assert.Equal(2, history.Count);
            Assert.All(history, h => Assert.Equal(deviceId1, h.DeviceId));
        }

        [Fact]
        public async Task DeviceHealthRepository_AddAsync_PreservesAllProperties()
        {
            var deviceId = Guid.NewGuid();
            DateTime capturedAt = DateTime.UtcNow;

            var health = new DeviceHealth
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                BatteryPercentage = 65m,
                BatteryVoltageValue = 4.2m,
                WifiSignalRssi = -55,
                WifiName = "MyNetwork",
                IsExternalPowerConnected = true,
                OtaStatus = "Up to date",
                IsOnline = true,
                LastHeartbeatUtc = DateTime.UtcNow,
                FirmwareVersion = "2.0.0",
                CapturedAtUtc = capturedAt
            };

            _ = await _repository.AddAsync(health, CancellationToken.None);
            DeviceHealth? retrieved = await _repository.GetLatestAsync(deviceId, CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal(deviceId, retrieved.DeviceId);
            Assert.Equal(65m, retrieved.BatteryPercentage);
            Assert.Equal(4.2m, retrieved.BatteryVoltageValue);
            Assert.Equal(-55, retrieved.WifiSignalRssi);
            Assert.Equal("MyNetwork", retrieved.WifiName);
            Assert.True(retrieved.IsExternalPowerConnected);
            Assert.Equal("Up to date", retrieved.OtaStatus);
            Assert.True(retrieved.IsOnline);
            Assert.Equal("2.0.0", retrieved.FirmwareVersion);
        }

        [Fact]
        public async Task DeviceHealthRepository_AddAsync_WithNullValues_Succeeds()
        {
            var deviceId = Guid.NewGuid();

            var health = new DeviceHealth
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                BatteryPercentage = null,
                WifiSignalRssi = null,
                WifiName = null,
                IsExternalPowerConnected = null,
                OtaStatus = null,
                IsOnline = null,
                LastHeartbeatUtc = null,
                FirmwareVersion = null,
                CapturedAtUtc = DateTime.UtcNow
            };

            DeviceHealth result = await _repository.AddAsync(health, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(deviceId, result.DeviceId);
            Assert.Null(result.BatteryPercentage);
            Assert.Null(result.WifiSignalRssi);
        }

        [Fact]
        public async Task DeviceHealthRepository_GetHistoryAsync_HandlesMultipleDevices()
        {
            var deviceId1 = Guid.NewGuid();
            var deviceId2 = Guid.NewGuid();
            DateTime now = DateTime.UtcNow;

            // Add multiple records for device 1
            for (int i = 0; i < 5; i++)
            {
                DeviceHealth health = TestDataBuilder.BuildDeviceHealth(deviceId1);
                health.CapturedAtUtc = now.AddMinutes(-i);
                _ = await _repository.AddAsync(health, CancellationToken.None);
            }

            // Add multiple records for device 2
            for (int i = 0; i < 3; i++)
            {
                DeviceHealth health = TestDataBuilder.BuildDeviceHealth(deviceId2);
                health.CapturedAtUtc = now.AddMinutes(-i);
                _ = await _repository.AddAsync(health, CancellationToken.None);
            }

            IReadOnlyList<DeviceHealth> history1 = await _repository.GetHistoryAsync(deviceId1, CancellationToken.None);
            IReadOnlyList<DeviceHealth> history2 = await _repository.GetHistoryAsync(deviceId2, CancellationToken.None);

            Assert.Equal(5, history1.Count);
            Assert.Equal(3, history2.Count);
        }
    }
}
