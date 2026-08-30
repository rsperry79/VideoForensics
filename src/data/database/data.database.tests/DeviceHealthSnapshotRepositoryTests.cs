using Microsoft.Extensions.Logging;
using Xunit;
using VideoForensics.Data.Database.Repositories;

namespace VideoForensics.Data.Database.Tests
{
    public class DeviceHealthSnapshotRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private DeviceHealthSnapshotRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            _repository = new DeviceHealthSnapshotRepository(_fixture.Factory, loggerFactory.CreateLogger<DeviceHealthSnapshotRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task AppendSnapshotAsync_CreatesSnapshot()
        {
            var deviceId = Guid.NewGuid();
            var snapshot = TestDataBuilder.BuildDeviceHealthSnapshot(deviceId);

            var result = await _repository.AppendSnapshotAsync(snapshot, CancellationToken.None);

            Assert.Equal(snapshot.Id, result.Id);
            var history = await _repository.GetHistoryAsync(deviceId, CancellationToken.None);
            var stored = Assert.Single(history);
            Assert.Equal(deviceId, stored.DeviceId);
            Assert.True(stored.Connected);
        }

        [Fact]
        public async Task GetLatestBeforeAsync_ReturnsNearestPriorSnapshot_NotFutureOne()
        {
            var deviceId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            var early = TestDataBuilder.BuildDeviceHealthSnapshot(deviceId);
            early.CapturedAtUtc = now.AddHours(-3);
            early.BatteryPercentage = 90m;

            var justBefore = TestDataBuilder.BuildDeviceHealthSnapshot(deviceId);
            justBefore.CapturedAtUtc = now.AddMinutes(-10);
            justBefore.BatteryPercentage = 8m;

            var future = TestDataBuilder.BuildDeviceHealthSnapshot(deviceId);
            future.CapturedAtUtc = now.AddHours(1);
            future.BatteryPercentage = 100m;

            await _repository.AppendSnapshotAsync(early, CancellationToken.None);
            await _repository.AppendSnapshotAsync(justBefore, CancellationToken.None);
            await _repository.AppendSnapshotAsync(future, CancellationToken.None);

            var result = await _repository.GetLatestBeforeAsync(deviceId, now, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(justBefore.Id, result!.Id);
            Assert.Equal(8m, result.BatteryPercentage);
        }

        [Fact]
        public async Task GetLatestBeforeAsync_ReturnsNull_WhenNoPriorSnapshotExists()
        {
            var deviceId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            var future = TestDataBuilder.BuildDeviceHealthSnapshot(deviceId);
            future.CapturedAtUtc = now.AddHours(1);
            await _repository.AppendSnapshotAsync(future, CancellationToken.None);

            var result = await _repository.GetLatestBeforeAsync(deviceId, now, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetHistoryAsync_PreservesMultipleSnapshotsPerDevice_NewestFirst()
        {
            var deviceId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            var snap1 = TestDataBuilder.BuildDeviceHealthSnapshot(deviceId);
            snap1.CapturedAtUtc = now.AddHours(-2);
            var snap2 = TestDataBuilder.BuildDeviceHealthSnapshot(deviceId);
            snap2.CapturedAtUtc = now.AddHours(-1);
            var snap3 = TestDataBuilder.BuildDeviceHealthSnapshot(deviceId);
            snap3.CapturedAtUtc = now;

            await _repository.AppendSnapshotAsync(snap1, CancellationToken.None);
            await _repository.AppendSnapshotAsync(snap2, CancellationToken.None);
            await _repository.AppendSnapshotAsync(snap3, CancellationToken.None);

            var history = await _repository.GetHistoryAsync(deviceId, CancellationToken.None);

            Assert.Equal(3, history.Count);
            Assert.Equal(snap3.Id, history[0].Id);
            Assert.Equal(snap2.Id, history[1].Id);
            Assert.Equal(snap1.Id, history[2].Id);
        }

        [Fact]
        public async Task AppendSnapshotAsync_AllowsNullDeviceIdAndDownloadEventId()
        {
            var snapshot = new VideoForensics.Data.Common.Entities.DeviceHealthSnapshot
            {
                Id = Guid.NewGuid(),
                DeviceId = null,
                DownloadEventId = null,
                CapturedAtUtc = DateTime.UtcNow
            };

            var result = await _repository.AppendSnapshotAsync(snapshot, CancellationToken.None);

            Assert.Null(result.DeviceId);
            Assert.Null(result.DownloadEventId);
        }
    }
}
