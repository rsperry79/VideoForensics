using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    public class UserRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private UserRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            _repository = new UserRepository(_fixture.Factory, loggerFactory.CreateLogger<UserRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task UserRepository_AddAndGet_RoundTrips()
        {
            User user = TestDataBuilder.BuildUser("provider_key_1", "John Doe", "john@example.com");

            await _repository.AddAsync(user, CancellationToken.None);
            User? retrieved = await _repository.GetAsync(user.Id, CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal(user.Id, retrieved.Id);
            Assert.Equal(user.ProviderUserKey, retrieved.ProviderUserKey);
            Assert.Equal(user.DisplayName, retrieved.DisplayName);
            Assert.Equal(user.Email, retrieved.Email);
        }

        [Fact]
        public async Task UserRepository_GetByProviderKey_FindsUser()
        {
            User user = TestDataBuilder.BuildUser("unique_key_123", "Jane Doe");

            await _repository.AddAsync(user, CancellationToken.None);
            User? retrieved = await _repository.GetByProviderKeyAsync("unique_key_123", CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal(user.Id, retrieved.Id);
        }

        [Fact]
        public async Task UserRepository_UpdateAsync_ModifiesData()
        {
            User user = TestDataBuilder.BuildUser("update_key", "Original Name");
            await _repository.AddAsync(user, CancellationToken.None);

            user.DisplayName = "Updated Name";
            await _repository.UpdateAsync(user, CancellationToken.None);

            User? retrieved = await _repository.GetAsync(user.Id, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.Equal("Updated Name", retrieved.DisplayName);
        }

        [Fact]
        public async Task UserRepository_DeleteAsync_RemovesUser()
        {
            User user = TestDataBuilder.BuildUser();
            await _repository.AddAsync(user, CancellationToken.None);

            await _repository.DeleteAsync(user.Id, CancellationToken.None);

            User? retrieved = await _repository.GetAsync(user.Id, CancellationToken.None);
            Assert.Null(retrieved);
        }

        [Fact]
        public async Task UserRepository_ListAsync_ReturnsAllUsers()
        {
            User user1 = TestDataBuilder.BuildUser();
            User user2 = TestDataBuilder.BuildUser();

            await _repository.AddAsync(user1, CancellationToken.None);
            await _repository.AddAsync(user2, CancellationToken.None);

            IReadOnlyList<User> list = await _repository.ListAsync(CancellationToken.None);
            Assert.Equal(2, list.Count);
        }

        [Fact]
        public async Task UserRepository_UniqueConstraint_DuplicateProviderKeyThrows()
        {
            User user1 = TestDataBuilder.BuildUser("dup_key", "User 1");
            User user2 = TestDataBuilder.BuildUser("dup_key", "User 2");

            await _repository.AddAsync(user1, CancellationToken.None);

            _ = await Assert.ThrowsAsync<DbUpdateException>(async () =>
                await _repository.AddAsync(user2, CancellationToken.None));
        }
    }
}
