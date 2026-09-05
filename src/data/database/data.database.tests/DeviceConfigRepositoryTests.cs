using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    public class DeviceConfigRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private DeviceConfigRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            _repository = new DeviceConfigRepository(_fixture.Factory, loggerFactory.CreateLogger<DeviceConfigRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task DeviceConfigRepository_AppendSnapshotAsync_CreatesSnapshot()
        {
            var deviceId = Guid.NewGuid();
            DeviceConfigSnapshot snapshot = TestDataBuilder.BuildDeviceConfigSnapshot(deviceId);

            _ = await _repository.AppendSnapshotAsync(snapshot, CancellationToken.None);
            DeviceConfigSnapshot? retrieved = await _repository.GetAsync(snapshot.Id, CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal(deviceId, retrieved.DeviceId);
            Assert.True(retrieved.MotionDetectionEnabled);
        }

        [Fact]
        public async Task DeviceConfigRepository_GetLatestAsync_ReturnsNewest()
        {
            var deviceId = Guid.NewGuid();
            DateTime now = DateTime.UtcNow;

            DeviceConfigSnapshot snap1 = TestDataBuilder.BuildDeviceConfigSnapshot(deviceId);
            snap1.CapturedAtUtc = now.AddHours(-2);

            DeviceConfigSnapshot snap2 = TestDataBuilder.BuildDeviceConfigSnapshot(deviceId);
            snap2.CapturedAtUtc = now.AddHours(-1);

            DeviceConfigSnapshot snap3 = TestDataBuilder.BuildDeviceConfigSnapshot(deviceId);
            snap3.CapturedAtUtc = now;

            _ = await _repository.AppendSnapshotAsync(snap1, CancellationToken.None);
            _ = await _repository.AppendSnapshotAsync(snap2, CancellationToken.None);
            _ = await _repository.AppendSnapshotAsync(snap3, CancellationToken.None);

            DeviceConfigSnapshot? latest = await _repository.GetLatestAsync(deviceId, CancellationToken.None);

            Assert.NotNull(latest);
            Assert.Equal(snap3.Id, latest.Id);
        }

        [Fact]
        public async Task DeviceConfigRepository_GetHistoryAsync_ReturnsInReverseOrder()
        {
            var deviceId = Guid.NewGuid();
            DateTime now = DateTime.UtcNow;

            DeviceConfigSnapshot snap1 = TestDataBuilder.BuildDeviceConfigSnapshot(deviceId);
            snap1.CapturedAtUtc = now.AddHours(-2);

            DeviceConfigSnapshot snap2 = TestDataBuilder.BuildDeviceConfigSnapshot(deviceId);
            snap2.CapturedAtUtc = now.AddHours(-1);

            DeviceConfigSnapshot snap3 = TestDataBuilder.BuildDeviceConfigSnapshot(deviceId);
            snap3.CapturedAtUtc = now;

            _ = await _repository.AppendSnapshotAsync(snap1, CancellationToken.None);
            _ = await _repository.AppendSnapshotAsync(snap2, CancellationToken.None);
            _ = await _repository.AppendSnapshotAsync(snap3, CancellationToken.None);

            IReadOnlyList<DeviceConfigSnapshot> history = await _repository.GetHistoryAsync(deviceId, CancellationToken.None);

            Assert.Equal(3, history.Count);
            Assert.Equal(snap3.Id, history[0].Id);
            Assert.Equal(snap2.Id, history[1].Id);
            Assert.Equal(snap1.Id, history[2].Id);
        }

        [Fact]
        public async Task DeviceConfigRepository_ListAsync_ReturnsAll()
        {
            DeviceConfigSnapshot snap1 = TestDataBuilder.BuildDeviceConfigSnapshot();
            DeviceConfigSnapshot snap2 = TestDataBuilder.BuildDeviceConfigSnapshot();

            _ = await _repository.AppendSnapshotAsync(snap1, CancellationToken.None);
            _ = await _repository.AppendSnapshotAsync(snap2, CancellationToken.None);

            IReadOnlyList<DeviceConfigSnapshot> list = await _repository.ListAsync(CancellationToken.None);
            Assert.Equal(2, list.Count);
        }

        [Fact]
        public async Task DeviceConfigRepository_AppendSnapshotAsync_AllowsMultiplePerDevice()
        {
            var deviceId = Guid.NewGuid();

            DeviceConfigSnapshot snap1 = TestDataBuilder.BuildDeviceConfigSnapshot(deviceId);
            DeviceConfigSnapshot snap2 = TestDataBuilder.BuildDeviceConfigSnapshot(deviceId);
            DeviceConfigSnapshot snap3 = TestDataBuilder.BuildDeviceConfigSnapshot(deviceId);

            _ = await _repository.AppendSnapshotAsync(snap1, CancellationToken.None);
            _ = await _repository.AppendSnapshotAsync(snap2, CancellationToken.None);
            _ = await _repository.AppendSnapshotAsync(snap3, CancellationToken.None);

            IReadOnlyList<DeviceConfigSnapshot> history = await _repository.GetHistoryAsync(deviceId, CancellationToken.None);
            Assert.Equal(3, history.Count);
        }
    }
}
