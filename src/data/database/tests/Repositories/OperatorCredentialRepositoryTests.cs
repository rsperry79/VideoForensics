using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
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

        /// <summary>Helper method to create and persist a parent operator for testing.</summary>
        private async Task<Guid> CreateOperatorAsync()
        {
            var operatorId = Guid.NewGuid();
            var @operator = new Operator
            {
                Id = operatorId,
                Username = "test_" + Guid.NewGuid().ToString("N")[..8],
                DisplayName = "Test Operator",
                FirstName = "Test",
                LastName = "Operator",
                Email = "test_" + Guid.NewGuid().ToString("N")[..8] + "@example.com",
                PasswordHash = "hashed",
                Role = OperatorRole.Admin,
                IsApproved = true,
                Active = true
            };

            await using (VideoForensicsDbContext db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                _ = db.Operators.Add(@operator);
                _ = await db.SaveChangesAsync(CancellationToken.None);
            }

            return operatorId;
        }

        #region AddAsync Tests

        [Fact]
        public async Task AddAsync_InsertsCredential_WithDefaults()
        {
            // Arrange: Create parent operator first
            var operatorId = Guid.NewGuid();
            var @operator = new Operator
            {
                Id = operatorId,
                Username = "test_" + Guid.NewGuid().ToString("N")[..8],
                DisplayName = "Test Operator",
                FirstName = "Test",
                LastName = "Operator",
                Email = "test@example.com",
                PasswordHash = "hashed",
                Role = OperatorRole.Admin,
                IsApproved = true,
                Active = true
            };

            await using (VideoForensicsDbContext db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                _ = db.Operators.Add(@operator);
                _ = await db.SaveChangesAsync(CancellationToken.None);
            }

            var credential = new OperatorCredential
            {
                Id = Guid.NewGuid(),
                OperatorId = operatorId,
                Label = "Test Credential",
                WebAuthnCredentialId = "test-cred-id-unique-1",
                WebAuthnPublicKey = new byte[] { 0x01, 0x02, 0x03 },
                CreatedAtUtc = DateTime.UtcNow,
                IsApproved = false  // Should default to false
            };

            // Act
            await _repository.AddAsync(credential, CancellationToken.None);

            // Assert: Verify credential was persisted
            await using (VideoForensicsDbContext db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                OperatorCredential? persisted = await db.OperatorCredentials.FindAsync(new object[] { credential.Id }, cancellationToken: CancellationToken.None);
                Assert.NotNull(persisted);
                Assert.Equal(credential.Id, persisted.Id);
                Assert.Equal(operatorId, persisted.OperatorId);
                Assert.Equal("Test Credential", persisted.Label);
                Assert.False(persisted.IsApproved);  // Should be false by default
                Assert.Null(persisted.RevokedAtUtc);  // Should not be revoked
            }
        }

        #endregion

        #region GetAsync Tests

        [Fact]
        public async Task GetAsync_ReturnsCredential_WhenFound()
        {
            // Arrange: Create parent operator and credential
            Guid operatorId = await CreateOperatorAsync();
            var credentialId = Guid.NewGuid();
            var credential = new OperatorCredential
            {
                Id = credentialId,
                OperatorId = operatorId,
                Label = "Test Credential",
                WebAuthnCredentialId = "test-cred-id-unique-2",
                WebAuthnPublicKey = new byte[] { 0x01, 0x02, 0x03 },
                CreatedAtUtc = DateTime.UtcNow,
                IsApproved = true,
                WebAuthnSignCount = 42
            };

            await using (VideoForensicsDbContext db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                _ = db.OperatorCredentials.Add(credential);
                _ = await db.SaveChangesAsync(CancellationToken.None);
            }

            // Act
            OperatorCredential? result = await _repository.GetAsync(credentialId, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(credentialId, result.Id);
            Assert.Equal(operatorId, result.OperatorId);
            Assert.Equal("Test Credential", result.Label);
            Assert.Equal(42u, result.WebAuthnSignCount);
        }

        [Fact]
        public async Task GetAsync_ReturnsNull_WhenNotFound()
        {
            // Act
            OperatorCredential? result = await _repository.GetAsync(Guid.NewGuid(), CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region GetByWebAuthnCredentialIdAsync Tests

        [Fact]
        public async Task GetByWebAuthnCredentialIdAsync_ReturnsCredential_WhenActive()
        {
            // Arrange: Create parent operator and an active (not revoked) credential
            Guid operatorId = await CreateOperatorAsync();
            var credentialId = Guid.NewGuid();
            string webAuthnId = "webauthn-id-unique-1";
            var credential = new OperatorCredential
            {
                Id = credentialId,
                OperatorId = operatorId,
                Label = "Active Credential",
                WebAuthnCredentialId = webAuthnId,
                WebAuthnPublicKey = new byte[] { 0x01, 0x02, 0x03 },
                CreatedAtUtc = DateTime.UtcNow,
                IsApproved = true,
                RevokedAtUtc = null  // Active
            };

            await using (VideoForensicsDbContext db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                _ = db.OperatorCredentials.Add(credential);
                _ = await db.SaveChangesAsync(CancellationToken.None);
            }

            // Act
            OperatorCredential? result = await _repository.GetByWebAuthnCredentialIdAsync(webAuthnId, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(credentialId, result.Id);
            Assert.Equal(webAuthnId, result.WebAuthnCredentialId);
        }

        [Fact]
        public async Task GetByWebAuthnCredentialIdAsync_ReturnsNull_WhenRevoked()
        {
            // Arrange: Create parent operator and a revoked credential
            Guid operatorId = await CreateOperatorAsync();
            var credentialId = Guid.NewGuid();
            string webAuthnId = "webauthn-id-unique-2";
            var credential = new OperatorCredential
            {
                Id = credentialId,
                OperatorId = operatorId,
                Label = "Revoked Credential",
                WebAuthnCredentialId = webAuthnId,
                WebAuthnPublicKey = new byte[] { 0x01, 0x02, 0x03 },
                CreatedAtUtc = DateTime.UtcNow,
                IsApproved = true,
                RevokedAtUtc = DateTime.UtcNow.AddHours(-1)  // Revoked
            };

            await using (VideoForensicsDbContext db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                _ = db.OperatorCredentials.Add(credential);
                _ = await db.SaveChangesAsync(CancellationToken.None);
            }

            // Act
            OperatorCredential? result = await _repository.GetByWebAuthnCredentialIdAsync(webAuthnId, CancellationToken.None);

            // Assert
            Assert.Null(result);  // Revoked credentials should not be returned
        }

        [Fact]
        public async Task GetByWebAuthnCredentialIdAsync_ReturnsNull_WhenNotFound()
        {
            // Act
            OperatorCredential? result = await _repository.GetByWebAuthnCredentialIdAsync("nonexistent-webauthn-id", CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region ListForOperatorAsync Tests

        [Fact]
        public async Task ListForOperatorAsync_ReturnsAllCredentials_ForOperator()
        {
            // Arrange: Create parent operator and multiple credentials for it
            Guid operatorId = await CreateOperatorAsync();
            var cred1 = new OperatorCredential
            {
                Id = Guid.NewGuid(),
                OperatorId = operatorId,
                Label = "Credential 1",
                WebAuthnCredentialId = "webauthn-id-3",
                WebAuthnPublicKey = new byte[] { 0x01 },
                CreatedAtUtc = DateTime.UtcNow.AddHours(-2),
                IsApproved = true
            };
            var cred2 = new OperatorCredential
            {
                Id = Guid.NewGuid(),
                OperatorId = operatorId,
                Label = "Credential 2",
                WebAuthnCredentialId = "webauthn-id-4",
                WebAuthnPublicKey = new byte[] { 0x02 },
                CreatedAtUtc = DateTime.UtcNow.AddHours(-1),
                IsApproved = true
            };
            var cred3 = new OperatorCredential
            {
                Id = Guid.NewGuid(),
                OperatorId = operatorId,
                Label = "Credential 3",
                WebAuthnCredentialId = "webauthn-id-5",
                WebAuthnPublicKey = new byte[] { 0x03 },
                CreatedAtUtc = DateTime.UtcNow,
                IsApproved = false  // Not approved, but still listed
            };

            await using (VideoForensicsDbContext db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                db.OperatorCredentials.AddRange(cred1, cred2, cred3);
                _ = await db.SaveChangesAsync(CancellationToken.None);
            }

            // Act
            IReadOnlyList<OperatorCredential> results = await _repository.ListForOperatorAsync(operatorId, CancellationToken.None);

            // Assert
            Assert.Equal(3, results.Count);
            // Should be ordered by creation time descending (newest first)
            Assert.Equal(cred3.Id, results[0].Id);
            Assert.Equal(cred2.Id, results[1].Id);
            Assert.Equal(cred1.Id, results[2].Id);
        }

        [Fact]
        public async Task ListForOperatorAsync_ReturnsEmpty_WhenOperatorHasNoCredentials()
        {
            // Act
            IReadOnlyList<OperatorCredential> results = await _repository.ListForOperatorAsync(Guid.NewGuid(), CancellationToken.None);

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public async Task ListForOperatorAsync_IncludesRevoked_Credentials()
        {
            // Arrange: Create parent operator and an approved and a revoked credential
            Guid operatorId = await CreateOperatorAsync();
            var approvedCred = new OperatorCredential
            {
                Id = Guid.NewGuid(),
                OperatorId = operatorId,
                Label = "Approved",
                WebAuthnCredentialId = "webauthn-id-6",
                WebAuthnPublicKey = new byte[] { 0x01 },
                CreatedAtUtc = DateTime.UtcNow.AddHours(-1),
                IsApproved = true,
                RevokedAtUtc = null
            };
            var revokedCred = new OperatorCredential
            {
                Id = Guid.NewGuid(),
                OperatorId = operatorId,
                Label = "Revoked",
                WebAuthnCredentialId = "webauthn-id-7",
                WebAuthnPublicKey = new byte[] { 0x02 },
                CreatedAtUtc = DateTime.UtcNow,
                IsApproved = true,
                RevokedAtUtc = DateTime.UtcNow
            };

            await using (VideoForensicsDbContext db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                db.OperatorCredentials.AddRange(approvedCred, revokedCred);
                _ = await db.SaveChangesAsync(CancellationToken.None);
            }

            // Act
            IReadOnlyList<OperatorCredential> results = await _repository.ListForOperatorAsync(operatorId, CancellationToken.None);

            // Assert: Both approved and revoked should be listed
            Assert.Equal(2, results.Count);
        }

        #endregion

        #region ListPendingApprovalAsync Tests

        [Fact]
        public async Task ListPendingApprovalAsync_ReturnsPendingCredentials_Only()
        {
            // Arrange: Create various credentials with different states
            Guid operatorId = await CreateOperatorAsync();
            Guid operator2Id = await CreateOperatorAsync();
            var pendingCred1 = new OperatorCredential
            {
                Id = Guid.NewGuid(),
                OperatorId = operatorId,
                Label = "Pending 1",
                WebAuthnCredentialId = "webauthn-id-8",
                WebAuthnPublicKey = new byte[] { 0x01 },
                CreatedAtUtc = DateTime.UtcNow.AddHours(-2),
                IsApproved = false  // Pending
            };
            var pendingCred2 = new OperatorCredential
            {
                Id = Guid.NewGuid(),
                OperatorId = operatorId,
                Label = "Pending 2",
                WebAuthnCredentialId = "webauthn-id-9",
                WebAuthnPublicKey = new byte[] { 0x02 },
                CreatedAtUtc = DateTime.UtcNow.AddHours(-1),
                IsApproved = false  // Pending
            };
            var approvedCred = new OperatorCredential
            {
                Id = Guid.NewGuid(),
                OperatorId = operatorId,
                Label = "Approved",
                WebAuthnCredentialId = "webauthn-id-10",
                WebAuthnPublicKey = new byte[] { 0x03 },
                CreatedAtUtc = DateTime.UtcNow,
                IsApproved = true  // Already approved
            };
            var revokedPendingCred = new OperatorCredential
            {
                Id = Guid.NewGuid(),
                OperatorId = operator2Id,
                Label = "Revoked Pending",
                WebAuthnCredentialId = "webauthn-id-11",
                WebAuthnPublicKey = new byte[] { 0x04 },
                CreatedAtUtc = DateTime.UtcNow,
                IsApproved = false,  // Was pending
                RevokedAtUtc = DateTime.UtcNow  // But got revoked
            };

            await using (VideoForensicsDbContext db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                db.OperatorCredentials.AddRange(pendingCred1, pendingCred2, approvedCred, revokedPendingCred);
                _ = await db.SaveChangesAsync(CancellationToken.None);
            }

            // Act
            IReadOnlyList<OperatorCredential> results = await _repository.ListPendingApprovalAsync(CancellationToken.None);

            // Assert: Only pending (not approved and not revoked) credentials
            Assert.Equal(2, results.Count);
            Assert.Contains(results, c => c.Id == pendingCred1.Id);
            Assert.Contains(results, c => c.Id == pendingCred2.Id);
            // Should be ordered by creation time ascending (oldest first)
            Assert.Equal(pendingCred1.Id, results[0].Id);
            Assert.Equal(pendingCred2.Id, results[1].Id);
        }

        [Fact]
        public async Task ListPendingApprovalAsync_ReturnsEmpty_WhenNoPendingCredentials()
        {
            // Arrange: Create parent operator and only approved credentials
            Guid operatorId = await CreateOperatorAsync();
            var approvedCred = new OperatorCredential
            {
                Id = Guid.NewGuid(),
                OperatorId = operatorId,
                Label = "Approved",
                WebAuthnCredentialId = "webauthn-id-12",
                WebAuthnPublicKey = new byte[] { 0x01 },
                CreatedAtUtc = DateTime.UtcNow,
                IsApproved = true
            };

            await using (VideoForensicsDbContext db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                _ = db.OperatorCredentials.Add(approvedCred);
                _ = await db.SaveChangesAsync(CancellationToken.None);
            }

            // Act
            IReadOnlyList<OperatorCredential> results = await _repository.ListPendingApprovalAsync(CancellationToken.None);

            // Assert
            Assert.Empty(results);
        }

        #endregion

        #region ApproveAsync Tests

        [Fact]
        public async Task ApproveAsync_SetsApprovedFlag_WhenCredentialExists()
        {
            // Arrange: Create parent operator and a pending credential
            Guid operatorId = await CreateOperatorAsync();
            var credentialId = Guid.NewGuid();
            var credential = new OperatorCredential
            {
                Id = credentialId,
                OperatorId = operatorId,
                Label = "Pending Credential",
                WebAuthnCredentialId = "webauthn-id-13",
                WebAuthnPublicKey = new byte[] { 0x01, 0x02, 0x03 },
                CreatedAtUtc = DateTime.UtcNow,
                IsApproved = false
            };

            await using (VideoForensicsDbContext db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                _ = db.OperatorCredentials.Add(credential);
                _ = await db.SaveChangesAsync(CancellationToken.None);
            }

            // Act
            await _repository.ApproveAsync(credentialId, CancellationToken.None);

            // Assert
            await using (VideoForensicsDbContext db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                OperatorCredential? updated = await db.OperatorCredentials.FindAsync(new object[] { credentialId }, cancellationToken: CancellationToken.None);
                Assert.NotNull(updated);
                Assert.True(updated.IsApproved);
            }
        }

        [Fact]
        public async Task ApproveAsync_DoesNothing_WhenCredentialDoesNotExist()
        {
            // Act
            await _repository.ApproveAsync(Guid.NewGuid(), CancellationToken.None);

            // Assert: No exception should be thrown
        }

        #endregion

        #region RevokeAsync Tests

        [Fact]
        public async Task RevokeAsync_SanitizesLogOutput_WhenReasonContainsNewlines()
        {
            // Arrange: Create parent operator and credential
            Guid operatorId = await CreateOperatorAsync();
            var credentialId = Guid.NewGuid();
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
            await using (VideoForensicsDbContext db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                _ = db.OperatorCredentials.Add(credential);
                _ = await db.SaveChangesAsync(CancellationToken.None);
            }

            // Act: Revoke with a reason containing CRLF (log injection attempt)
            string injectedReason = "User requested revocation\r\nFAKE ADMIN LOG ENTRY: Unauthorized access granted";
            await _repository.RevokeAsync(credentialId, injectedReason, CancellationToken.None);

            // Assert: Verify the credential was revoked and the reason is stored as-is in the database
            await using (VideoForensicsDbContext db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                OperatorCredential? revokedCredential = await db.OperatorCredentials.FindAsync(new object[] { credentialId }, cancellationToken: CancellationToken.None);
                Assert.NotNull(revokedCredential);
                _ = Assert.NotNull(revokedCredential.RevokedAtUtc);
                // The raw reason (with newlines) should be stored in the database
                Assert.Equal(injectedReason, revokedCredential.RevokedReason);
            }
        }

        [Fact]
        public async Task RevokeAsync_SuccessfullyRevokes_WhenCredentialExists()
        {
            // Arrange: Create parent operator and credential
            Guid operatorId = await CreateOperatorAsync();
            var credentialId = Guid.NewGuid();
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
            await using (VideoForensicsDbContext db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                _ = db.OperatorCredentials.Add(credential);
                _ = await db.SaveChangesAsync(CancellationToken.None);
            }

            // Act: Revoke with a simple reason
            string reason = "User requested revocation";
            await _repository.RevokeAsync(credentialId, reason, CancellationToken.None);

            // Assert: Verify the credential was revoked
            await using (VideoForensicsDbContext db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                OperatorCredential? revokedCredential = await db.OperatorCredentials.FindAsync(new object[] { credentialId }, cancellationToken: CancellationToken.None);
                Assert.NotNull(revokedCredential);
                _ = Assert.NotNull(revokedCredential.RevokedAtUtc);
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
            await using VideoForensicsDbContext db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None);
            OperatorCredential? credential = await db.OperatorCredentials.FindAsync(new object[] { nonexistentId }, cancellationToken: CancellationToken.None);
            Assert.Null(credential);
        }

        #endregion

        #region RecordSuccessfulAuthAsync Tests

        [Fact]
        public async Task RecordSuccessfulAuthAsync_UpdatesSignCountAndLastUsedTime()
        {
            // Arrange: Create parent operator and credential
            Guid operatorId = await CreateOperatorAsync();
            var credentialId = Guid.NewGuid();
            var credential = new OperatorCredential
            {
                Id = credentialId,
                OperatorId = operatorId,
                Label = "Test Credential",
                WebAuthnCredentialId = "webauthn-id-14",
                WebAuthnPublicKey = new byte[] { 0x01, 0x02, 0x03 },
                CreatedAtUtc = DateTime.UtcNow,
                IsApproved = true,
                WebAuthnSignCount = 0,
                LastUsedAtUtc = null
            };

            await using (VideoForensicsDbContext db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                _ = db.OperatorCredentials.Add(credential);
                _ = await db.SaveChangesAsync(CancellationToken.None);
            }

            // Act: Record a successful authentication with new signature count
            DateTime beforeCall = DateTime.UtcNow;
            await _repository.RecordSuccessfulAuthAsync(credentialId, 42, CancellationToken.None);
            DateTime afterCall = DateTime.UtcNow;

            // Assert
            await using (VideoForensicsDbContext db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                OperatorCredential? updated = await db.OperatorCredentials.FindAsync(new object[] { credentialId }, cancellationToken: CancellationToken.None);
                Assert.NotNull(updated);
                Assert.Equal(42u, updated.WebAuthnSignCount);
                _ = Assert.NotNull(updated.LastUsedAtUtc);
                Assert.True(updated.LastUsedAtUtc >= beforeCall);
                Assert.True(updated.LastUsedAtUtc <= afterCall);
            }
        }

        [Fact]
        public async Task RecordSuccessfulAuthAsync_DoesNothing_WhenCredentialDoesNotExist()
        {
            // Act
            await _repository.RecordSuccessfulAuthAsync(Guid.NewGuid(), 42, CancellationToken.None);

            // Assert: No exception should be thrown
        }

        #endregion

        #region MarkFirstLoginNotifiedAsync Tests

        [Fact]
        public async Task MarkFirstLoginNotifiedAsync_SetsNotificationTime()
        {
            // Arrange: Create parent operator and credential
            Guid operatorId = await CreateOperatorAsync();
            var credentialId = Guid.NewGuid();
            var credential = new OperatorCredential
            {
                Id = credentialId,
                OperatorId = operatorId,
                Label = "Test Credential",
                WebAuthnCredentialId = "webauthn-id-15",
                WebAuthnPublicKey = new byte[] { 0x01, 0x02, 0x03 },
                CreatedAtUtc = DateTime.UtcNow,
                IsApproved = true,
                FirstLoginNotifiedAtUtc = null
            };

            await using (VideoForensicsDbContext db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                _ = db.OperatorCredentials.Add(credential);
                _ = await db.SaveChangesAsync(CancellationToken.None);
            }

            // Act
            DateTime beforeCall = DateTime.UtcNow;
            await _repository.MarkFirstLoginNotifiedAsync(credentialId, CancellationToken.None);
            DateTime afterCall = DateTime.UtcNow;

            // Assert
            await using (VideoForensicsDbContext db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                OperatorCredential? updated = await db.OperatorCredentials.FindAsync(new object[] { credentialId }, cancellationToken: CancellationToken.None);
                Assert.NotNull(updated);
                _ = Assert.NotNull(updated.FirstLoginNotifiedAtUtc);
                Assert.True(updated.FirstLoginNotifiedAtUtc >= beforeCall);
                Assert.True(updated.FirstLoginNotifiedAtUtc <= afterCall);
            }
        }

        [Fact]
        public async Task MarkFirstLoginNotifiedAsync_DoesNothing_WhenCredentialDoesNotExist()
        {
            // Act
            await _repository.MarkFirstLoginNotifiedAsync(Guid.NewGuid(), CancellationToken.None);

            // Assert: No exception should be thrown
        }

        #endregion

        #region Concurrent Safety Tests

        [Fact]
        public async Task AddAsync_HandlesConcurrentInserts_ForSameOperator()
        {
            // Arrange: Create parent operator and prepare two credentials for concurrent insert
            Guid operatorId = await CreateOperatorAsync();
            var cred1 = new OperatorCredential
            {
                Id = Guid.NewGuid(),
                OperatorId = operatorId,
                Label = "Concurrent 1",
                WebAuthnCredentialId = "webauthn-concurrent-1",
                WebAuthnPublicKey = new byte[] { 0x01 },
                CreatedAtUtc = DateTime.UtcNow
            };
            var cred2 = new OperatorCredential
            {
                Id = Guid.NewGuid(),
                OperatorId = operatorId,
                Label = "Concurrent 2",
                WebAuthnCredentialId = "webauthn-concurrent-2",
                WebAuthnPublicKey = new byte[] { 0x02 },
                CreatedAtUtc = DateTime.UtcNow
            };

            // Act: Insert both concurrently
            Task task1 = _repository.AddAsync(cred1, CancellationToken.None);
            Task task2 = _repository.AddAsync(cred2, CancellationToken.None);
            await Task.WhenAll(task1, task2);

            // Assert: Both should be persisted without conflicts
            await using VideoForensicsDbContext db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None);
            int count = db.OperatorCredentials.Count(c => c.OperatorId == operatorId);
            Assert.Equal(2, count);
        }

        #endregion
    }
}
