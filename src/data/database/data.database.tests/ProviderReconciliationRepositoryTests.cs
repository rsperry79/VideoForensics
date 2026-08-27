using Microsoft.Extensions.Logging;
using Xunit;
using VideoForensics.Data.Database.Repositories;

namespace VideoForensics.Data.Database.Tests
{
    public class ProviderReconciliationRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private ProviderReconciliationRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            _repository = new ProviderReconciliationRepository(_fixture.Factory, loggerFactory.CreateLogger<ProviderReconciliationRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task ProviderReconciliationRepository_AppendAsync_CreatesRecord()
        {
            var deviceId = Guid.NewGuid();
            var record = TestDataBuilder.BuildProviderReconciliationRecord(deviceId, "evt_001");

            await _repository.AppendAsync(record, CancellationToken.None);
            var retrieved = await _repository.GetAsync(record.Id, CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal(deviceId, retrieved.DeviceId);
            Assert.Equal("evt_001", retrieved.ProviderEventId);
        }

        [Fact]
        public async Task ProviderReconciliationRepository_GetHistoryForDevice_ReturnsInReverseOrder()
        {
            var deviceId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            var rec1 = TestDataBuilder.BuildProviderReconciliationRecord(deviceId);
            rec1.RanAtUtc = now.AddHours(-2);

            var rec2 = TestDataBuilder.BuildProviderReconciliationRecord(deviceId);
            rec2.RanAtUtc = now.AddHours(-1);

            var rec3 = TestDataBuilder.BuildProviderReconciliationRecord(deviceId);
            rec3.RanAtUtc = now;

            await _repository.AppendAsync(rec1, CancellationToken.None);
            await _repository.AppendAsync(rec2, CancellationToken.None);
            await _repository.AppendAsync(rec3, CancellationToken.None);

            var history = await _repository.GetHistoryForDeviceAsync(deviceId, CancellationToken.None);

            Assert.Equal(3, history.Count);
            Assert.Equal(rec3.Id, history[0].Id);
            Assert.Equal(rec2.Id, history[1].Id);
            Assert.Equal(rec1.Id, history[2].Id);
        }

        [Fact]
        public async Task ProviderReconciliationRepository_ListAsync_ReturnsAll()
        {
            var rec1 = TestDataBuilder.BuildProviderReconciliationRecord();
            var rec2 = TestDataBuilder.BuildProviderReconciliationRecord();

            await _repository.AppendAsync(rec1, CancellationToken.None);
            await _repository.AppendAsync(rec2, CancellationToken.None);

            var list = await _repository.ListAsync(CancellationToken.None);
            Assert.Equal(2, list.Count);
        }

        [Fact]
        public async Task ProviderReconciliationRepository_GetOpenDiscrepanciesAsync_ReturnsAll()
        {
            var rec1 = TestDataBuilder.BuildProviderReconciliationRecord();
            var rec2 = TestDataBuilder.BuildProviderReconciliationRecord();

            await _repository.AppendAsync(rec1, CancellationToken.None);
            await _repository.AppendAsync(rec2, CancellationToken.None);

            var discrepancies = await _repository.GetOpenDiscrepanciesAsync(CancellationToken.None);
            Assert.Equal(2, discrepancies.Count);
        }

        [Fact]
        public async Task ProviderReconciliationRepository_AllowsMultipleRecordsPerDevice()
        {
            var deviceId = Guid.NewGuid();

            var rec1 = TestDataBuilder.BuildProviderReconciliationRecord(deviceId, "evt_1");
            var rec2 = TestDataBuilder.BuildProviderReconciliationRecord(deviceId, "evt_2");
            var rec3 = TestDataBuilder.BuildProviderReconciliationRecord(deviceId, "evt_3");

            await _repository.AppendAsync(rec1, CancellationToken.None);
            await _repository.AppendAsync(rec2, CancellationToken.None);
            await _repository.AppendAsync(rec3, CancellationToken.None);

            var history = await _repository.GetHistoryForDeviceAsync(deviceId, CancellationToken.None);
            Assert.Equal(3, history.Count);
        }
    }
}
