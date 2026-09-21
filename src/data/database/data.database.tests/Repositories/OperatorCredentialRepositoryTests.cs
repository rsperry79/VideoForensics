using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    public class OperatorCredentialRepositoryTests : RepositoryTestBase
    {
        private OperatorCredentialRepository _repository = null!;

        public override async ValueTask InitializeAsync()
        {
            await base.InitializeAsync();
            _repository = new OperatorCredentialRepository(Fixture.Factory, CreateLogger<OperatorCredentialRepository>());
        }

        #region RevokeAsync Tests

        [Fact]
        public async Task RevokeAsync_SanitizesLogOutput_WhenReasonContainsNewlines()
        {
            // Arrange: Create an operator credential
            var credentialId = Guid.NewGuid();
            var operatorId = Guid.NewGuid();
            var credential = new OperatorCredential
            {
                Id = credentialId,
                OperatorId = operatorId,
                Label = "Test Credential",
                WebAuthnCredentialId = "test-cred-id",
                WebAuthnPublicKey = new byte[] { 0x01, 0x02, 0x03 },
                CreatedAtUtc = DateTime.UtcNow,
                IsApproved = true
            };

            // Add the credential to the database first
            await using (var db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                _ = db.OperatorCredentials.Add(credential);
                _ = await db.SaveChangesAsync(CancellationToken.None);
            }

            // Act: Revoke with a reason containing CRLF (log injection attempt)
            string injectedReason = "User requested revocation\r\nFAKE ADMIN LOG ENTRY: Unauthorized access granted";
            await _repository.RevokeAsync(credentialId, injectedReason, CancellationToken.None);

            // Assert: Verify the credential was revoked and the reason is stored as-is in the database
            await using (var db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                OperatorCredential? revokedCredential = await db.OperatorCredentials.FindAsync(new object[] { credentialId }, cancellationToken: CancellationToken.None);
                Assert.NotNull(revokedCredential);
                Assert.NotNull(revokedCredential.RevokedAtUtc);
                // The raw reason (with newlines) should be stored in the database
                Assert.Equal(injectedReason, revokedCredential.RevokedReason);
            }
        }

        [Fact]
        public async Task RevokeAsync_SuccessfullyRevokes_WhenCredentialExists()
        {
            // Arrange: Create an operator credential
            var credentialId = Guid.NewGuid();
            var operatorId = Guid.NewGuid();
            var credential = new OperatorCredential
            {
                Id = credentialId,
                OperatorId = operatorId,
                Label = "Test Credential",
                WebAuthnCredentialId = "test-cred-id-2",
                WebAuthnPublicKey = new byte[] { 0x04, 0x05, 0x06 },
                CreatedAtUtc = DateTime.UtcNow,
                IsApproved = true
            };

            // Add the credential to the database first
            await using (var db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                _ = db.OperatorCredentials.Add(credential);
                _ = await db.SaveChangesAsync(CancellationToken.None);
            }

            // Act: Revoke with a simple reason
            string reason = "User requested revocation";
            await _repository.RevokeAsync(credentialId, reason, CancellationToken.None);

            // Assert: Verify the credential was revoked
            await using (var db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                OperatorCredential? revokedCredential = await db.OperatorCredentials.FindAsync(new object[] { credentialId }, cancellationToken: CancellationToken.None);
                Assert.NotNull(revokedCredential);
                Assert.NotNull(revokedCredential.RevokedAtUtc);
                Assert.Equal(reason, revokedCredential.RevokedReason);
            }
        }

        [Fact]
        public async Task RevokeAsync_DoesNothing_WhenCredentialDoesNotExist()
        {
            // Arrange: Use a non-existent credential ID
            var nonexistentId = Guid.NewGuid();

            // Act: Revoke a non-existent credential
            await _repository.RevokeAsync(nonexistentId, "Some reason", CancellationToken.None);

            // Assert: No exception thrown, and database is unchanged
            await using (var db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                OperatorCredential? credential = await db.OperatorCredentials.FindAsync(new object[] { nonexistentId }, cancellationToken: CancellationToken.None);
                Assert.Null(credential);
            }
        }

        #endregion
    }
}
