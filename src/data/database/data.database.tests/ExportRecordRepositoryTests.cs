using Microsoft.Extensions.Logging;
using Xunit;
using VideoForensics.Data.Database.Repositories;

namespace VideoForensics.Data.Database.Tests
{
    public class ExportRecordRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private ExportRecordRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            _repository = new ExportRecordRepository(_fixture.Factory, loggerFactory.CreateLogger<ExportRecordRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task ExportRecordRepository_AppendAsync_CreatesRecordAndItems()
        {
            var record = TestDataBuilder.BuildExportRecord("TestUser");
            var item1 = TestDataBuilder.BuildExportRecordItem(record.Id);
            var item2 = TestDataBuilder.BuildExportRecordItem(record.Id);
            var items = new[] { item1, item2 };

            await _repository.AppendAsync(record, items, CancellationToken.None);
            var retrieved = await _repository.GetAsync(record.Id, CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal("TestUser", retrieved.ExportedByUserName);
            Assert.Equal("CASE-2026-001", retrieved.CaseReference);
        }

        [Fact]
        public async Task ExportRecordRepository_GetItemsForRecord_ReturnsAllItems()
        {
            var record = TestDataBuilder.BuildExportRecord();
            var item1 = TestDataBuilder.BuildExportRecordItem(record.Id, Guid.NewGuid());
            var item2 = TestDataBuilder.BuildExportRecordItem(record.Id, Guid.NewGuid());
            var item3 = TestDataBuilder.BuildExportRecordItem(Guid.NewGuid(), Guid.NewGuid());

            await _repository.AppendAsync(record, new[] { item1, item2 }, CancellationToken.None);
            await _repository.AppendAsync(TestDataBuilder.BuildExportRecord(), new[] { item3 }, CancellationToken.None);

            var items = await _repository.GetItemsForRecordAsync(record.Id, CancellationToken.None);

            Assert.Equal(2, items.Count);
        }

        [Fact]
        public async Task ExportRecordRepository_GetHistoryForMediaItem_ReturnsExportsContainingItem()
        {
            var mediaItemId = Guid.NewGuid();
            var otherMediaItemId = Guid.NewGuid();

            var record1 = TestDataBuilder.BuildExportRecord();
            var item1 = TestDataBuilder.BuildExportRecordItem(record1.Id, mediaItemId);
            var item2 = TestDataBuilder.BuildExportRecordItem(record1.Id, otherMediaItemId);

            var record2 = TestDataBuilder.BuildExportRecord();
            var item3 = TestDataBuilder.BuildExportRecordItem(record2.Id, mediaItemId);

            await _repository.AppendAsync(record1, new[] { item1, item2 }, CancellationToken.None);
            await _repository.AppendAsync(record2, new[] { item3 }, CancellationToken.None);

            var history = await _repository.GetHistoryForMediaItemAsync(mediaItemId, CancellationToken.None);

            Assert.Equal(2, history.Count);
        }

        [Fact]
        public async Task ExportRecordRepository_GetHistoryForDevice_ReturnsRecords()
        {
            var record1 = TestDataBuilder.BuildExportRecord();
            var record2 = TestDataBuilder.BuildExportRecord();

            var item1 = TestDataBuilder.BuildExportRecordItem(record1.Id);
            var item2 = TestDataBuilder.BuildExportRecordItem(record2.Id);

            await _repository.AppendAsync(record1, new[] { item1 }, CancellationToken.None);
            await _repository.AppendAsync(record2, new[] { item2 }, CancellationToken.None);

            var history = await _repository.GetHistoryForDeviceAsync(Guid.NewGuid(), CancellationToken.None);

            Assert.NotEmpty(history);
        }

        [Fact]
        public async Task ExportRecordRepository_ListAsync_ReturnsAll()
        {
            var record1 = TestDataBuilder.BuildExportRecord();
            var record2 = TestDataBuilder.BuildExportRecord();

            var item1 = TestDataBuilder.BuildExportRecordItem(record1.Id);
            var item2 = TestDataBuilder.BuildExportRecordItem(record2.Id);

            await _repository.AppendAsync(record1, new[] { item1 }, CancellationToken.None);
            await _repository.AppendAsync(record2, new[] { item2 }, CancellationToken.None);

            var list = await _repository.ListAsync(CancellationToken.None);
            Assert.Equal(2, list.Count);
        }

        [Fact]
        public async Task ExportRecordRepository_HistoriesAreInReverseChronologicalOrder()
        {
            var now = DateTime.UtcNow;
            var mediaItemId = Guid.NewGuid();

            var record1 = TestDataBuilder.BuildExportRecord();
            record1.ExportedAtUtc = now.AddHours(-2);

            var record2 = TestDataBuilder.BuildExportRecord();
            record2.ExportedAtUtc = now.AddHours(-1);

            var record3 = TestDataBuilder.BuildExportRecord();
            record3.ExportedAtUtc = now;

            var item1 = TestDataBuilder.BuildExportRecordItem(record1.Id, mediaItemId);
            var item2 = TestDataBuilder.BuildExportRecordItem(record2.Id, mediaItemId);
            var item3 = TestDataBuilder.BuildExportRecordItem(record3.Id, mediaItemId);

            await _repository.AppendAsync(record1, new[] { item1 }, CancellationToken.None);
            await _repository.AppendAsync(record2, new[] { item2 }, CancellationToken.None);
            await _repository.AppendAsync(record3, new[] { item3 }, CancellationToken.None);

            var history = await _repository.GetHistoryForMediaItemAsync(mediaItemId, CancellationToken.None);

            Assert.Equal(3, history.Count);
            Assert.Equal(record3.Id, history[0].Id);
            Assert.Equal(record2.Id, history[1].Id);
            Assert.Equal(record1.Id, history[2].Id);
        }
    }
}
