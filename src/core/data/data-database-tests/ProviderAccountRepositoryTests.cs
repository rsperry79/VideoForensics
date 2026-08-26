using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Xunit;
using VideoForensics.Data.Database.Repositories;

namespace VideoForensics.Data.Database.Tests
{
    public class ProviderAccountRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private ProviderAccountRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            _repository = new ProviderAccountRepository(_fixture.Factory, loggerFactory.CreateLogger<ProviderAccountRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task ProviderAccountRepository_AddAndGet_RoundTrips()
        {
            var userId = Guid.NewGuid();
            var account = TestDataBuilder.BuildProviderAccount(userId, "Ring");

            await _repository.AddAsync(account, CancellationToken.None);
            var retrieved = await _repository.GetAsync(account.Id, CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal(account.Id, retrieved.Id);
            Assert.Equal(userId, retrieved.UserId);
            Assert.Equal("Ring", retrieved.ProviderName);
            Assert.True(retrieved.IsActive);
        }

        [Fact]
        public async Task ProviderAccountRepository_GetByUserAndProvider_FindsAccount()
        {
            var userId = Guid.NewGuid();
            var account = TestDataBuilder.BuildProviderAccount(userId, "Wyze");

            await _repository.AddAsync(account, CancellationToken.None);
            var retrieved = await _repository.GetByUserAndProviderAsync(userId, "Wyze", CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal(account.Id, retrieved.Id);
        }

        [Fact]
        public async Task ProviderAccountRepository_GetByUserId_ReturnsAllForUser()
        {
            var userId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();

            var account1 = TestDataBuilder.BuildProviderAccount(userId, "Ring");
            var account2 = TestDataBuilder.BuildProviderAccount(userId, "Wyze");
            var account3 = TestDataBuilder.BuildProviderAccount(otherUserId, "Ring");

            await _repository.AddAsync(account1, CancellationToken.None);
            await _repository.AddAsync(account2, CancellationToken.None);
            await _repository.AddAsync(account3, CancellationToken.None);

            var list = await _repository.GetByUserIdAsync(userId, CancellationToken.None);
            Assert.Equal(2, list.Count);
        }

        [Fact]
        public async Task ProviderAccountRepository_UpdateAsync_ModifiesData()
        {
            var account = TestDataBuilder.BuildProviderAccount();
            await _repository.AddAsync(account, CancellationToken.None);

            account.IsActive = false;
            await _repository.UpdateAsync(account, CancellationToken.None);

            var retrieved = await _repository.GetAsync(account.Id, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.False(retrieved.IsActive);
        }

        [Fact]
        public async Task ProviderAccountRepository_DeleteAsync_RemovesAccount()
        {
            var account = TestDataBuilder.BuildProviderAccount();
            await _repository.AddAsync(account, CancellationToken.None);

            await _repository.DeleteAsync(account.Id, CancellationToken.None);

            var retrieved = await _repository.GetAsync(account.Id, CancellationToken.None);
            Assert.Null(retrieved);
        }

        [Fact]
        public async Task ProviderAccountRepository_ListAsync_ReturnsAll()
        {
            var account1 = TestDataBuilder.BuildProviderAccount();
            var account2 = TestDataBuilder.BuildProviderAccount();

            await _repository.AddAsync(account1, CancellationToken.None);
            await _repository.AddAsync(account2, CancellationToken.None);

            var list = await _repository.ListAsync(CancellationToken.None);
            Assert.Equal(2, list.Count);
        }

        [Fact]
        public async Task ProviderAccountRepository_ListActiveAsync_ReturnsOnlyActive()
        {
            var account1 = TestDataBuilder.BuildProviderAccount();
            account1.IsActive = true;

            var account2 = TestDataBuilder.BuildProviderAccount();
            account2.IsActive = false;

            await _repository.AddAsync(account1, CancellationToken.None);
            await _repository.AddAsync(account2, CancellationToken.None);

            var list = await _repository.ListActiveAsync(CancellationToken.None);
            Assert.Single(list);
            Assert.Equal(account1.Id, list[0].Id);
        }

        [Fact]
        public async Task ProviderAccountRepository_UniqueConstraint_DuplicateUserProviderComboThrows()
        {
            var userId = Guid.NewGuid();
            var account1 = TestDataBuilder.BuildProviderAccount(userId, "Ring");
            var account2 = TestDataBuilder.BuildProviderAccount(userId, "Ring");

            await _repository.AddAsync(account1, CancellationToken.None);

            await Assert.ThrowsAsync<DbUpdateException>(async () =>
                await _repository.AddAsync(account2, CancellationToken.None));
        }
    }
}
