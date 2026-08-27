using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Xunit;
using VideoForensics.Data.Database.Repositories;

namespace VideoForensics.Data.Database.Tests
{
    public class DownloadEventRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private DownloadEventRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            _repository = new DownloadEventRepository(_fixture.Factory, loggerFactory.CreateLogger<DownloadEventRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task DownloadEventRepository_AddAndGet_RoundTrips()
        {
            var deviceId = Guid.NewGuid();
            var evt = TestDataBuilder.BuildDownloadEvent(deviceId, "evt_001", true);

            await _repository.AddAsync(evt, CancellationToken.None);
            var retrieved = await _repository.GetAsync(evt.Id, CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal(evt.Id, retrieved.Id);
            Assert.Equal(deviceId, retrieved.DeviceId);
            Assert.Equal("evt_001", retrieved.ProviderEventId);
            Assert.True(retrieved.Success);
        }

        [Fact]
        public async Task DownloadEventRepository_ExistsForProviderEventId_ReturnsTrue()
        {
            var deviceId = Guid.NewGuid();
            var evt = TestDataBuilder.BuildDownloadEvent(deviceId, "evt_002");

            await _repository.AddAsync(evt, CancellationToken.None);
            var exists = await _repository.ExistsForProviderEventIdAsync(deviceId, "evt_002", CancellationToken.None);

            Assert.True(exists);
        }

        [Fact]
        public async Task DownloadEventRepository_ExistsForProviderEventId_ReturnsFalseForNonexistent()
        {
            var deviceId = Guid.NewGuid();
            var exists = await _repository.ExistsForProviderEventIdAsync(deviceId, "nonexistent", CancellationToken.None);

            Assert.False(exists);
        }

        [Fact]
        public async Task DownloadEventRepository_GetByProviderEventId_FindsEvent()
        {
            var deviceId = Guid.NewGuid();
            var evt = TestDataBuilder.BuildDownloadEvent(deviceId, "evt_003");

            await _repository.AddAsync(evt, CancellationToken.None);
            var retrieved = await _repository.GetByProviderEventIdAsync(deviceId, "evt_003", CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal(evt.Id, retrieved.Id);
        }

        [Fact]
        public async Task DownloadEventRepository_GetLatestSuccessfulEventTime_ReturnsMaxOfSuccessfulEvents()
        {
            var deviceId = Guid.NewGuid();
            var baseTime = DateTime.UtcNow;

            var evt1 = TestDataBuilder.BuildDownloadEvent(deviceId, "evt_1", true);
            evt1.EventOccurredAtUtc = baseTime.AddHours(-3);
            evt1.DownloadCompletedUtc = baseTime.AddHours(-3);

            var evt2 = TestDataBuilder.BuildDownloadEvent(deviceId, "evt_2", true);
            evt2.EventOccurredAtUtc = baseTime.AddHours(-1);
            evt2.DownloadCompletedUtc = baseTime.AddHours(-1);

            var evt3 = TestDataBuilder.BuildDownloadEvent(deviceId, "evt_3", false);
            evt3.EventOccurredAtUtc = baseTime.AddHours(1);
            evt3.DownloadCompletedUtc = null;

            await _repository.AddAsync(evt1, CancellationToken.None);
            await _repository.AddAsync(evt2, CancellationToken.None);
            await _repository.AddAsync(evt3, CancellationToken.None);

            var watermark = await _repository.GetLatestSuccessfulEventTimeAsync(deviceId, CancellationToken.None);

            Assert.NotNull(watermark);
            Assert.Equal(evt2.EventOccurredAtUtc, watermark.Value);
        }

        [Fact]
        public async Task DownloadEventRepository_GetLatestSuccessfulEventTime_IgnoresFailedEvents()
        {
            var deviceId = Guid.NewGuid();
            var baseTime = DateTime.UtcNow;

            var failed1 = TestDataBuilder.BuildDownloadEvent(deviceId, "fail_1", false);
            failed1.EventOccurredAtUtc = baseTime.AddHours(10);
            failed1.DownloadCompletedUtc = null;

            var success1 = TestDataBuilder.BuildDownloadEvent(deviceId, "success_1", true);
            success1.EventOccurredAtUtc = baseTime.AddHours(-1);
            success1.DownloadCompletedUtc = baseTime.AddHours(-1);

            await _repository.AddAsync(failed1, CancellationToken.None);
            await _repository.AddAsync(success1, CancellationToken.None);

            var watermark = await _repository.GetLatestSuccessfulEventTimeAsync(deviceId, CancellationToken.None);

            Assert.NotNull(watermark);
            Assert.Equal(success1.EventOccurredAtUtc, watermark.Value);
        }

        [Fact]
        public async Task DownloadEventRepository_GetLatestSuccessfulEventTime_IgnoresEventsWithoutDownloadCompletedUtc()
        {
            var deviceId = Guid.NewGuid();
            var baseTime = DateTime.UtcNow;

            var incomplete = TestDataBuilder.BuildDownloadEvent(deviceId, "incomplete", true);
            incomplete.EventOccurredAtUtc = baseTime.AddHours(10);
            incomplete.DownloadCompletedUtc = null;

            var complete = TestDataBuilder.BuildDownloadEvent(deviceId, "complete", true);
            complete.EventOccurredAtUtc = baseTime.AddHours(-1);
            complete.DownloadCompletedUtc = baseTime.AddHours(-1);

            await _repository.AddAsync(incomplete, CancellationToken.None);
            await _repository.AddAsync(complete, CancellationToken.None);

            var watermark = await _repository.GetLatestSuccessfulEventTimeAsync(deviceId, CancellationToken.None);

            Assert.NotNull(watermark);
            Assert.Equal(complete.EventOccurredAtUtc, watermark.Value);
        }

        [Fact]
        public async Task DownloadEventRepository_GetLatestSuccessfulEventTime_ReturnsNullWhenNoSuccessfulEvents()
        {
            var deviceId = Guid.NewGuid();

            var evt = TestDataBuilder.BuildDownloadEvent(deviceId, "evt", false);
            await _repository.AddAsync(evt, CancellationToken.None);

            var watermark = await _repository.GetLatestSuccessfulEventTimeAsync(deviceId, CancellationToken.None);

            Assert.Null(watermark);
        }

        [Fact]
        public async Task DownloadEventRepository_GetByDeviceId_ReturnsAllForDevice()
        {
            var deviceId = Guid.NewGuid();
            var otherDeviceId = Guid.NewGuid();

            var evt1 = TestDataBuilder.BuildDownloadEvent(deviceId);
            var evt2 = TestDataBuilder.BuildDownloadEvent(deviceId);
            var evt3 = TestDataBuilder.BuildDownloadEvent(otherDeviceId);

            await _repository.AddAsync(evt1, CancellationToken.None);
            await _repository.AddAsync(evt2, CancellationToken.None);
            await _repository.AddAsync(evt3, CancellationToken.None);

            var list = await _repository.GetByDeviceIdAsync(deviceId, CancellationToken.None);
            Assert.Equal(2, list.Count);
        }

        [Fact]
        public async Task DownloadEventRepository_UpdateAsync_ModifiesData()
        {
            var evt = TestDataBuilder.BuildDownloadEvent();
            await _repository.AddAsync(evt, CancellationToken.None);

            evt.Success = false;
            evt.ErrorMessage = "Test error";
            await _repository.UpdateAsync(evt, CancellationToken.None);

            var retrieved = await _repository.GetAsync(evt.Id, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.False(retrieved.Success);
            Assert.Equal("Test error", retrieved.ErrorMessage);
        }

        [Fact]
        public async Task DownloadEventRepository_DeleteAsync_RemovesEvent()
        {
            var evt = TestDataBuilder.BuildDownloadEvent();
            await _repository.AddAsync(evt, CancellationToken.None);

            await _repository.DeleteAsync(evt.Id, CancellationToken.None);

            var retrieved = await _repository.GetAsync(evt.Id, CancellationToken.None);
            Assert.Null(retrieved);
        }

        [Fact]
        public async Task DownloadEventRepository_ListAsync_ReturnsAll()
        {
            var evt1 = TestDataBuilder.BuildDownloadEvent();
            var evt2 = TestDataBuilder.BuildDownloadEvent();

            await _repository.AddAsync(evt1, CancellationToken.None);
            await _repository.AddAsync(evt2, CancellationToken.None);

            var list = await _repository.ListAsync(CancellationToken.None);
            Assert.Equal(2, list.Count);
        }

        [Fact]
        public async Task DownloadEventRepository_UniqueConstraint_DuplicateDeviceEventIdComboThrows()
        {
            var deviceId = Guid.NewGuid();
            var evt1 = TestDataBuilder.BuildDownloadEvent(deviceId, "dup_evt");
            var evt2 = TestDataBuilder.BuildDownloadEvent(deviceId, "dup_evt");

            await _repository.AddAsync(evt1, CancellationToken.None);

            await Assert.ThrowsAsync<DbUpdateException>(async () =>
                await _repository.AddAsync(evt2, CancellationToken.None));
        }
    }
}
