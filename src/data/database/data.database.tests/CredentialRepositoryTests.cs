using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Xunit;
using VideoForensics.Data.Database.Repositories;

namespace VideoForensics.Data.Database.Tests
{
    public class CredentialRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private CredentialRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            _repository = new CredentialRepository(
                _fixture.Factory,
                _fixture.EncryptionProvider,
                loggerFactory.CreateLogger<CredentialRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task CredentialRepository_SetAndGet_RoundTripsPlaintext()
        {
            var accountId = Guid.NewGuid();
            var plainPassword = "MySecurePassword123!";

            await _repository.SetAsync(accountId, "Password", plainPassword, CancellationToken.None);
            var retrieved = await _repository.GetAsync(accountId, "Password", CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal("Password", retrieved.Value.CredentialType);
            Assert.Equal(plainPassword, retrieved.Value.DecryptedValue);
        }

        [Fact]
        public async Task CredentialRepository_SetAsync_StoresEncryptedValue()
        {
            var accountId = Guid.NewGuid();
            var plainToken = "refresh_token_abc123";

            await _repository.SetAsync(accountId, "RefreshToken", plainToken, CancellationToken.None);

            var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            var ctx = _fixture.Factory.CreateDbContext();
            var stored = await ctx.Credentials.FirstOrDefaultAsync(
                c => c.ProviderAccountId == accountId && c.CredentialType == "RefreshToken");

            Assert.NotNull(stored);
            Assert.NotEqual(plainToken, stored.EncryptedValue);
        }

        [Fact]
        public async Task CredentialRepository_GetAsync_ReturnsNullForNonexistent()
        {
            var accountId = Guid.NewGuid();
            var retrieved = await _repository.GetAsync(accountId, "NonExistent", CancellationToken.None);

            Assert.Null(retrieved);
        }

        [Fact]
        public async Task CredentialRepository_SetAsync_UpdatesExistingCredential()
        {
            var accountId = Guid.NewGuid();

            await _repository.SetAsync(accountId, "Password", "old_password", CancellationToken.None);
            await _repository.SetAsync(accountId, "Password", "new_password", CancellationToken.None);

            var retrieved = await _repository.GetAsync(accountId, "Password", CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.Equal("new_password", retrieved.Value.DecryptedValue);
        }

        [Fact]
        public async Task CredentialRepository_SetAsync_UpdatesSetsRotatedUtc()
        {
            var accountId = Guid.NewGuid();

            await _repository.SetAsync(accountId, "Password", "first", CancellationToken.None);
            var beforeUpdate = DateTime.UtcNow;
            await _repository.SetAsync(accountId, "Password", "second", CancellationToken.None);

            var ctx = _fixture.Factory.CreateDbContext();
            var stored = await ctx.Credentials.FirstOrDefaultAsync(
                c => c.ProviderAccountId == accountId && c.CredentialType == "Password");

            Assert.NotNull(stored);
            Assert.NotNull(stored.RotatedUtc);
            Assert.True(stored.RotatedUtc >= beforeUpdate);
        }

        [Fact]
        public async Task CredentialRepository_GetByProviderAccountId_ReturnsAllForAccount()
        {
            var accountId = Guid.NewGuid();
            var otherAccountId = Guid.NewGuid();

            await _repository.SetAsync(accountId, "Password", "pwd", CancellationToken.None);
            await _repository.SetAsync(accountId, "RefreshToken", "token", CancellationToken.None);
            await _repository.SetAsync(otherAccountId, "Password", "other_pwd", CancellationToken.None);

            var list = await _repository.GetByProviderAccountIdAsync(accountId, CancellationToken.None);

            Assert.Equal(2, list.Count);
        }

        [Fact]
        public async Task CredentialRepository_DeleteAsync_RemovesCredential()
        {
            var accountId = Guid.NewGuid();

            await _repository.SetAsync(accountId, "Password", "pwd", CancellationToken.None);
            await _repository.DeleteAsync(accountId, "Password", CancellationToken.None);

            var retrieved = await _repository.GetAsync(accountId, "Password", CancellationToken.None);
            Assert.Null(retrieved);
        }

        [Fact]
        public async Task CredentialRepository_DeleteByProviderAccountId_RemovesAllForAccount()
        {
            var accountId = Guid.NewGuid();
            var otherAccountId = Guid.NewGuid();

            await _repository.SetAsync(accountId, "Password", "pwd", CancellationToken.None);
            await _repository.SetAsync(accountId, "RefreshToken", "token", CancellationToken.None);
            await _repository.SetAsync(otherAccountId, "Password", "other", CancellationToken.None);

            await _repository.DeleteByProviderAccountIdAsync(accountId, CancellationToken.None);

            var list = await _repository.GetByProviderAccountIdAsync(accountId, CancellationToken.None);
            Assert.Empty(list);

            var otherList = await _repository.GetByProviderAccountIdAsync(otherAccountId, CancellationToken.None);
            Assert.Single(otherList);
        }

        [Fact]
        public async Task CredentialRepository_UniqueConstraint_DuplicateAccountTypeComboThrows()
        {
            var accountId = Guid.NewGuid();

            await _repository.SetAsync(accountId, "Password", "pwd1", CancellationToken.None);

            var ctx = _fixture.Factory.CreateDbContext();
            var cred = new VideoForensics.Data.Common.Entities.Credential
            {
                Id = Guid.NewGuid(),
                ProviderAccountId = accountId,
                CredentialType = "Password",
                EncryptedValue = "some_encrypted_value",
                EncryptionProvider = "DataProtection",
                CreatedUtc = DateTime.UtcNow
            };

            ctx.Credentials.Add(cred);

            await Assert.ThrowsAsync<DbUpdateException>(async () =>
                await ctx.SaveChangesAsync());
        }

        [Fact]
        public async Task CredentialRepository_DifferentTypes_CanCoexist()
        {
            var accountId = Guid.NewGuid();

            await _repository.SetAsync(accountId, "Password", "pwd", CancellationToken.None);
            await _repository.SetAsync(accountId, "RefreshToken", "token", CancellationToken.None);

            var pwd = await _repository.GetAsync(accountId, "Password", CancellationToken.None);
            var token = await _repository.GetAsync(accountId, "RefreshToken", CancellationToken.None);

            Assert.NotNull(pwd);
            Assert.NotNull(token);
            Assert.Equal("pwd", pwd.Value.DecryptedValue);
            Assert.Equal("token", token.Value.DecryptedValue);
        }
    }
}
