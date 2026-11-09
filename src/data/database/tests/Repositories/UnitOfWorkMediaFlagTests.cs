using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;
using VideoForensics.Utils.Testing;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    /// <summary>Flag writes made through the unit of work commit, or roll back, together with the unit.</summary>
    public class UnitOfWorkMediaFlagTests : IAsyncLifetime
    {
        private static readonly DateTime T0 = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        private SqliteInMemoryFixture _fixture = null!;
        private UnitOfWork _unitOfWork = null!;
        private MediaFlagRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = LoggerFactory.Create(b => { });
            var services = new ServiceCollection();
            _ = services.AddSingleton<ILogger<UnitOfWork>>(new Logger<UnitOfWork>(loggerFactory));
            _unitOfWork = new UnitOfWork(_fixture.Factory, services.BuildServiceProvider(), loggerFactory.CreateLogger<UnitOfWork>());
            _repository = new MediaFlagRepository(_unitOfWork, NullLogger<MediaFlagRepository>.Instance);
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        private static MediaFlag Raised(Guid device)
        {
            return new()
            {
                FlagType = MediaFlagType.RssiDeviation,
                DeviceId = device,
                WindowStartUtc = T0,
                WindowEndUtc = T0.AddMinutes(1),
                RaisedAtUtc = T0,
            };
        }

        [Fact]
        public async Task UnitOfWorkMediaFlagRepository_UpsertThenReview_CommitsFlagAndAuditTogether()
        {
            var device = Guid.NewGuid();

            Guid id = await _unitOfWork.ExecuteAsync(async ctx =>
            {
                MediaFlag flag = await new MediaFlagRepository(_unitOfWork, NullLogger<MediaFlagRepository>.Instance).UpsertRaisedAsync(Raised(device), CancellationToken.None);
                _ = await new MediaFlagRepository(_unitOfWork, NullLogger<MediaFlagRepository>.Instance).UpsertRaisedAsync(Raised(device), CancellationToken.None);
                _ = await new MediaFlagRepository(_unitOfWork, NullLogger<MediaFlagRepository>.Instance).UpdateStatusAsync(flag.Id, MediaFlagStatus.Unreviewed, MediaFlagStatus.Kept, null, "alice", CancellationToken.None);
                return flag.Id;
            }, CancellationToken.None);

            MediaFlag? stored = await _repository.GetAsync(id, CancellationToken.None);
            Assert.Equal(MediaFlagStatus.Kept, stored?.Status);
            Assert.Equal(1, await _repository.CountAsync(new MediaFlagFilter(), CancellationToken.None));
            var actionLog = new ActionLogRepository(_unitOfWork, NullLogger<ActionLogRepository>.Instance);
            _ = Assert.Single(await actionLog.GetHistoryForEntityAsync("MediaFlag", id, CancellationToken.None));
        }

        [Fact]
        public async Task UnitOfWorkMediaFlagRepository_WorkThrows_RollsBackFlagAndAudit()
        {
            var device = Guid.NewGuid();

            _ = await Assert.ThrowsAsync<InvalidOperationException>(() => _unitOfWork.ExecuteAsync<int>(async ctx =>
            {
                MediaFlag flag = await new MediaFlagRepository(_unitOfWork, NullLogger<MediaFlagRepository>.Instance).UpsertRaisedAsync(Raised(device), CancellationToken.None);
                _ = await new MediaFlagRepository(_unitOfWork, NullLogger<MediaFlagRepository>.Instance).UpdateStatusAsync(flag.Id, MediaFlagStatus.Unreviewed, MediaFlagStatus.Kept, null, "alice", CancellationToken.None);
                throw new InvalidOperationException("boom");
            }, CancellationToken.None));

            Assert.Equal(0, await _repository.CountAsync(new MediaFlagFilter(), CancellationToken.None));
            var actionLog = new ActionLogRepository(_unitOfWork, NullLogger<ActionLogRepository>.Instance);
            Assert.Empty(await actionLog.ListAsync(CancellationToken.None));
        }
    }
}
