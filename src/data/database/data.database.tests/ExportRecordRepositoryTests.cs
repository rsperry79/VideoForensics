using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

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
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
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
            ExportRecord record = TestDataBuilder.BuildExportRecord("TestUser");
            ExportRecordItem item1 = TestDataBuilder.BuildExportRecordItem(record.Id);
            ExportRecordItem item2 = TestDataBuilder.BuildExportRecordItem(record.Id);
            ExportRecordItem[] items = new[] { item1, item2 };

            _ = await _repository.AppendAsync(record, items, CancellationToken.None);
            ExportRecord? retrieved = await _repository.GetAsync(record.Id, CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal("TestUser", retrieved.ExportedByUserName);
            Assert.Equal("CASE-2026-001", retrieved.CaseReference);
        }

        [Fact]
        public async Task ExportRecordRepository_GetItemsForRecord_ReturnsAllItems()
        {
            ExportRecord record = TestDataBuilder.BuildExportRecord();
            ExportRecordItem item1 = TestDataBuilder.BuildExportRecordItem(record.Id, Guid.NewGuid());
            ExportRecordItem item2 = TestDataBuilder.BuildExportRecordItem(record.Id, Guid.NewGuid());
            ExportRecordItem item3 = TestDataBuilder.BuildExportRecordItem(Guid.NewGuid(), Guid.NewGuid());

            _ = await _repository.AppendAsync(record, new[] { item1, item2 }, CancellationToken.None);
            _ = await _repository.AppendAsync(TestDataBuilder.BuildExportRecord(), new[] { item3 }, CancellationToken.None);

            IReadOnlyList<ExportRecordItem> items = await _repository.GetItemsForRecordAsync(record.Id, CancellationToken.None);

            Assert.Equal(2, items.Count);
        }

        [Fact]
        public async Task ExportRecordRepository_GetHistoryForMediaItem_ReturnsExportsContainingItem()
        {
            var mediaItemId = Guid.NewGuid();
            var otherMediaItemId = Guid.NewGuid();

            ExportRecord record1 = TestDataBuilder.BuildExportRecord();
            ExportRecordItem item1 = TestDataBuilder.BuildExportRecordItem(record1.Id, mediaItemId);
            ExportRecordItem item2 = TestDataBuilder.BuildExportRecordItem(record1.Id, otherMediaItemId);

            ExportRecord record2 = TestDataBuilder.BuildExportRecord();
            ExportRecordItem item3 = TestDataBuilder.BuildExportRecordItem(record2.Id, mediaItemId);

            _ = await _repository.AppendAsync(record1, new[] { item1, item2 }, CancellationToken.None);
            _ = await _repository.AppendAsync(record2, new[] { item3 }, CancellationToken.None);

            IReadOnlyList<ExportRecord> history = await _repository.GetHistoryForMediaItemAsync(mediaItemId, CancellationToken.None);

            Assert.Equal(2, history.Count);
        }

        [Fact]
        public async Task ExportRecordRepository_GetHistoryForDevice_ReturnsRecords()
        {
            ExportRecord record1 = TestDataBuilder.BuildExportRecord();
            ExportRecord record2 = TestDataBuilder.BuildExportRecord();

            ExportRecordItem item1 = TestDataBuilder.BuildExportRecordItem(record1.Id);
            ExportRecordItem item2 = TestDataBuilder.BuildExportRecordItem(record2.Id);

            _ = await _repository.AppendAsync(record1, new[] { item1 }, CancellationToken.None);
            _ = await _repository.AppendAsync(record2, new[] { item2 }, CancellationToken.None);

            IReadOnlyList<ExportRecord> history = await _repository.GetHistoryForDeviceAsync(Guid.NewGuid(), CancellationToken.None);

            Assert.NotEmpty(history);
        }

        [Fact]
        public async Task ExportRecordRepository_ListAsync_ReturnsAll()
        {
            ExportRecord record1 = TestDataBuilder.BuildExportRecord();
            ExportRecord record2 = TestDataBuilder.BuildExportRecord();

            ExportRecordItem item1 = TestDataBuilder.BuildExportRecordItem(record1.Id);
            ExportRecordItem item2 = TestDataBuilder.BuildExportRecordItem(record2.Id);

            _ = await _repository.AppendAsync(record1, new[] { item1 }, CancellationToken.None);
            _ = await _repository.AppendAsync(record2, new[] { item2 }, CancellationToken.None);

            IReadOnlyList<ExportRecord> list = await _repository.ListAsync(CancellationToken.None);
            Assert.Equal(2, list.Count);
        }

        [Fact]
        public async Task ExportRecordRepository_HistoriesAreInReverseChronologicalOrder()
        {
            DateTime now = DateTime.UtcNow;
            var mediaItemId = Guid.NewGuid();

            ExportRecord record1 = TestDataBuilder.BuildExportRecord();
            record1.ExportedAtUtc = now.AddHours(-2);

            ExportRecord record2 = TestDataBuilder.BuildExportRecord();
            record2.ExportedAtUtc = now.AddHours(-1);

            ExportRecord record3 = TestDataBuilder.BuildExportRecord();
            record3.ExportedAtUtc = now;

            ExportRecordItem item1 = TestDataBuilder.BuildExportRecordItem(record1.Id, mediaItemId);
            ExportRecordItem item2 = TestDataBuilder.BuildExportRecordItem(record2.Id, mediaItemId);
            ExportRecordItem item3 = TestDataBuilder.BuildExportRecordItem(record3.Id, mediaItemId);

            _ = await _repository.AppendAsync(record1, new[] { item1 }, CancellationToken.None);
            _ = await _repository.AppendAsync(record2, new[] { item2 }, CancellationToken.None);
            _ = await _repository.AppendAsync(record3, new[] { item3 }, CancellationToken.None);

            IReadOnlyList<ExportRecord> history = await _repository.GetHistoryForMediaItemAsync(mediaItemId, CancellationToken.None);

            Assert.Equal(3, history.Count);
            Assert.Equal(record3.Id, history[0].Id);
            Assert.Equal(record2.Id, history[1].Id);
            Assert.Equal(record1.Id, history[2].Id);
        }
    }
}
