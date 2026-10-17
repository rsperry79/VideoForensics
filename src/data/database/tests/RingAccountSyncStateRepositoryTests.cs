using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>
    /// Config-sync gate state on RingAccounts: the default interval is 24 hours, marking a completed sync stores its
    /// UTC time, and an unknown account reads as never-synced without throwing.
    /// </summary>
    public class RingAccountSyncStateRepositoryTests : IAsyncLifetime
    {
        private static readonly Guid ProviderAccountId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        private SqliteInMemoryFixture _fixture = null!;
        private RingAccountSyncStateRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            _repository = new RingAccountSyncStateRepository(_fixture.Factory);

            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            _ = db.RingAccounts.Add(new RingAccount { Id = Guid.NewGuid(), ProviderAccountId = ProviderAccountId, SubscriptionLevel = "unknown" });
            _ = await db.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task RingAccountSyncStateRepository_GetConfigSyncStateAsync_NewAccount_ReturnsDefaultIntervalOf24Hours()
        {
            RingConfigSyncState state = await _repository.GetConfigSyncStateAsync(ProviderAccountId, CancellationToken.None);

            Assert.Equal(24, state.IntervalHours);
            Assert.Null(state.LastConfigSyncUtc);
        }

        [Fact]
        public async Task RingAccountSyncStateRepository_MarkConfigSyncCompletedAsync_ValidAccount_SetsLastConfigSyncUtc()
        {
            var completedAt = new DateTime(2026, 10, 16, 12, 30, 0, DateTimeKind.Utc);

            await _repository.MarkConfigSyncCompletedAsync(ProviderAccountId, completedAt, CancellationToken.None);

            RingConfigSyncState state = await _repository.GetConfigSyncStateAsync(ProviderAccountId, CancellationToken.None);
            Assert.Equal(completedAt, state.LastConfigSyncUtc);
        }

        [Fact]
        public async Task RingAccountSyncStateRepository_GetConfigSyncStateAsync_StoredIntervalBelowOne_ReturnsMinimumOfOne()
        {
            await using (VideoForensicsDbContext db = _fixture.Factory.CreateDbContext())
            {
                RingAccount account = await db.RingAccounts.SingleAsync(a => a.ProviderAccountId == ProviderAccountId);
                account.ConfigSyncIntervalHours = 0;
                _ = await db.SaveChangesAsync();
            }

            RingConfigSyncState state = await _repository.GetConfigSyncStateAsync(ProviderAccountId, CancellationToken.None);

            Assert.Equal(1, state.IntervalHours);
        }

        [Fact]
        public async Task RingAccountSyncStateRepository_GetConfigSyncStateAsync_UnknownAccount_ReturnsDefaultsWithoutThrowing()
        {
            RingConfigSyncState state = await _repository.GetConfigSyncStateAsync(Guid.NewGuid(), CancellationToken.None);

            Assert.Equal(24, state.IntervalHours);
            Assert.Null(state.LastConfigSyncUtc);
        }
    }
}
