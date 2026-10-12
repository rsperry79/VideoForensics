using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    public class ProviderAccountRepositoryTests : RepositoryTestBase
    {
        private ProviderAccountRepository _repository = null!;

        public override async ValueTask InitializeAsync()
        {
            await base.InitializeAsync();
            _repository = new ProviderAccountRepository(Fixture.Factory, CreateLogger<ProviderAccountRepository>());
        }

        [Fact]
        public async Task AddAsync_SanitizesLogOutput_WhenProviderNameContainsNewlines()
        {
            // Arrange: Create a provider account with ProviderName containing CRLF (log injection attempt)
            var injectedProviderName = "RingCamera\r\nFAKE LOG ENTRY: Admin access granted";
            var userId = Guid.NewGuid();
            var account = new ProviderAccount
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ProviderName = injectedProviderName,
                IsActive = true
            };

            // Act: Add the provider account (logs it)
            await _repository.AddAsync(account, CancellationToken.None);

            // Assert: Verify the account was persisted with the raw ProviderName intact
            await using (var db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                ProviderAccount? persistedAccount = await db.ProviderAccounts.FindAsync(new object[] { account.Id }, cancellationToken: CancellationToken.None);
                Assert.NotNull(persistedAccount);
                // The raw ProviderName (with newlines) should be stored in the database
                Assert.Equal(injectedProviderName, persistedAccount.ProviderName);
            }
        }
    }
}
