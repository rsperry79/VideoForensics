using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    public class NoticeRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private NoticeRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            _repository = new NoticeRepository(_fixture.Factory, loggerFactory.CreateLogger<NoticeRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task NoticeRepository_ListForOperatorAsync_ExcludesAdminsOnlyNoticeWhenNotAdmin()
        {
            var operatorId = Guid.NewGuid();

            Notice allAudienceNotice = TestDataBuilder.BuildNotice("AllAudienceEvent", audience: 0, operatorId: operatorId);
            Notice adminsOnlyNotice = TestDataBuilder.BuildNotice("AdminsOnlyEvent", audience: 1, operatorId: operatorId);

            await _repository.AddAsync(allAudienceNotice, CancellationToken.None);
            await _repository.AddAsync(adminsOnlyNotice, CancellationToken.None);

            IReadOnlyList<Notice> result = await _repository.ListForOperatorAsync(operatorId, isAdmin: false, includeDismissed: false, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal(allAudienceNotice.Id, result[0].Id);
            Assert.Equal("AllAudienceEvent", result[0].EventType);
        }

        [Fact]
        public async Task NoticeRepository_ListForOperatorAsync_IncludesAdminsOnlyNoticeWhenAdmin()
        {
            var operatorId = Guid.NewGuid();

            Notice allAudienceNotice = TestDataBuilder.BuildNotice("AllAudienceEvent", audience: 0, operatorId: operatorId);
            Notice adminsOnlyNotice = TestDataBuilder.BuildNotice("AdminsOnlyEvent", audience: 1, operatorId: operatorId);

            await _repository.AddAsync(allAudienceNotice, CancellationToken.None);
            await _repository.AddAsync(adminsOnlyNotice, CancellationToken.None);

            IReadOnlyList<Notice> result = await _repository.ListForOperatorAsync(operatorId, isAdmin: true, includeDismissed: false, CancellationToken.None);

            Assert.Equal(2, result.Count);
            Assert.Contains(result, n => n.Id == allAudienceNotice.Id);
            Assert.Contains(result, n => n.Id == adminsOnlyNotice.Id);
        }

        [Fact]
        public async Task NoticeRepository_CountUndismissedForOperatorAsync_ExcludesAdminsOnlyNoticeWhenNotAdmin()
        {
            var operatorId = Guid.NewGuid();

            Notice allAudienceNotice = TestDataBuilder.BuildNotice("AllAudienceEvent", audience: 0, operatorId: operatorId);
            Notice adminsOnlyNotice = TestDataBuilder.BuildNotice("AdminsOnlyEvent", audience: 1, operatorId: operatorId);

            await _repository.AddAsync(allAudienceNotice, CancellationToken.None);
            await _repository.AddAsync(adminsOnlyNotice, CancellationToken.None);

            int result = await _repository.CountUndismissedForOperatorAsync(operatorId, isAdmin: false, CancellationToken.None);

            Assert.Equal(1, result);
        }

        [Fact]
        public async Task NoticeRepository_CountUndismissedForOperatorAsync_IncludesAdminsOnlyNoticeWhenAdmin()
        {
            var operatorId = Guid.NewGuid();

            Notice allAudienceNotice = TestDataBuilder.BuildNotice("AllAudienceEvent", audience: 0, operatorId: operatorId);
            Notice adminsOnlyNotice = TestDataBuilder.BuildNotice("AdminsOnlyEvent", audience: 1, operatorId: operatorId);

            await _repository.AddAsync(allAudienceNotice, CancellationToken.None);
            await _repository.AddAsync(adminsOnlyNotice, CancellationToken.None);

            int result = await _repository.CountUndismissedForOperatorAsync(operatorId, isAdmin: true, CancellationToken.None);

            Assert.Equal(2, result);
        }
    }
}
