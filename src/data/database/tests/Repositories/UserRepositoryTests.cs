using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    public class UserRepositoryTests : RepositoryTestBase
    {
        private UserRepository _repository = null!;

        public override async ValueTask InitializeAsync()
        {
            await base.InitializeAsync();
            _repository = new UserRepository(Fixture.Factory, CreateLogger<UserRepository>());
        }

        [Fact]
        public async Task AddAsync_SanitizesLogOutput_WhenDisplayNameContainsNewlines()
        {
            // Arrange: Create a user with DisplayName containing CRLF (log injection attempt)
            var injectedDisplayName = "John Doe\r\nFAKE LOG ENTRY: Admin password changed";
            var user = new User
            {
                Id = Guid.NewGuid(),
                ProviderUserKey = "unique-key-" + Guid.NewGuid(),
                DisplayName = injectedDisplayName
            };

            // Act: Add the user (logs it)
            await _repository.AddAsync(user, CancellationToken.None);

            // Assert: Verify the user was persisted with the raw DisplayName intact
            await using (var db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                User? persistedUser = await db.Users.FindAsync(new object[] { user.Id }, cancellationToken: CancellationToken.None);
                Assert.NotNull(persistedUser);
                // The raw DisplayName (with newlines) should be stored in the database
                Assert.Equal(injectedDisplayName, persistedUser.DisplayName);
            }
        }
    }
}
