using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
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
        public async Task UserRepository_AddAsync_MasksEmailInLogs_ButPreservesinDatabase()
        {
            // Arrange: Create a user with an email address as DisplayName
            const string emailAddress = "jane.doe@example.com";
            User user = TestDataBuilder.BuildUser("provider_key_email_test", emailAddress);

            // Mock the logger to capture log calls
            Mock<ILogger<UserRepository>> mockLogger = new();

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync(CancellationToken.None);
            UserRepository repositoryWithMockedLogger = new(
                _fixture.Factory,
                mockLogger.Object
            );

            // Act: Add the user
            await repositoryWithMockedLogger.AddAsync(user, CancellationToken.None);

            // Assert: Verify that the raw email is NOT in any logged message
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(emailAddress)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never,
                "Email address should not appear in logs"
            );

            // Assert: Verify that the raw email IS preserved in the database
            User? retrieved = await _repository.GetAsync(user.Id, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.Equal(emailAddress, retrieved.DisplayName);
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
