using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    public class OperatorRepositoryTests : RepositoryTestBase
    {
        private OperatorRepository _repository = null!;

        public override async ValueTask InitializeAsync()
        {
            await base.InitializeAsync();
            _repository = new OperatorRepository(Fixture.Factory, CreateLogger<OperatorRepository>());
        }

        [Fact]
        public async Task AddAsync_SanitizesLogOutput_WhenDisplayNameContainsNewlines()
        {
            // Arrange: Create an operator with DisplayName containing CRLF (log injection attempt)
            var injectedDisplayName = "Alice Johnson\r\nFAKE LOG ENTRY: SuperAdmin account created";
            var @operator = new Operator
            {
                Id = Guid.NewGuid(),
                Username = "alice_" + Guid.NewGuid().ToString("N")[..8],
                DisplayName = injectedDisplayName,
                FirstName = "Alice",
                LastName = "Johnson",
                Email = "alice@example.com",
                PasswordHash = "hashed_password",
                Role = OperatorRole.SuperAdmin,
                IsApproved = true,
                Active = true
            };

            // Act: Add the operator (logs it)
            await _repository.AddAsync(@operator, CancellationToken.None);

            // Assert: Verify the operator was persisted with the raw DisplayName intact
            await using (var db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                Operator? persistedOperator = await db.Operators.FindAsync(new object[] { @operator.Id }, cancellationToken: CancellationToken.None);
                Assert.NotNull(persistedOperator);
                // The raw DisplayName (with newlines) should be stored in the database
                Assert.Equal(injectedDisplayName, persistedOperator.DisplayName);
            }
        }

        [Fact]
        public async Task IncrementFailedLoginAttemptAsync_BelowThreshold_DoesNotLock()
        {
            // Arrange: Create an operator
            var operatorId = Guid.NewGuid();
            var @operator = new Operator
            {
                Id = operatorId,
                Username = "test_" + Guid.NewGuid().ToString("N")[..8],
                DisplayName = "Test User",
                FirstName = "Test",
                LastName = "User",
                Email = "test@example.com",
                PasswordHash = "hashed",
                Role = OperatorRole.Admin,
                IsApproved = true,
                Active = true
            };

            await _repository.AddAsync(@operator, CancellationToken.None);

            // Act: Increment to 4 of 5 max attempts
            await _repository.IncrementFailedLoginAttemptAsync(operatorId, maxFailedAttempts: 5, lockoutDurationMinutes: 15, CancellationToken.None);
            await _repository.IncrementFailedLoginAttemptAsync(operatorId, maxFailedAttempts: 5, lockoutDurationMinutes: 15, CancellationToken.None);
            await _repository.IncrementFailedLoginAttemptAsync(operatorId, maxFailedAttempts: 5, lockoutDurationMinutes: 15, CancellationToken.None);
            await _repository.IncrementFailedLoginAttemptAsync(operatorId, maxFailedAttempts: 5, lockoutDurationMinutes: 15, CancellationToken.None);

            // Assert
            var result = await _repository.GetAsync(operatorId, CancellationToken.None);
            Assert.NotNull(result);
            Assert.Equal(4, result.FailedLoginAttemptCount);
            Assert.Null(result.LockedOutUntilUtc);
        }

        [Fact]
        public async Task IncrementFailedLoginAttemptAsync_ReachesThreshold_SetLockout()
        {
            // Arrange: Create an operator
            var operatorId = Guid.NewGuid();
            var @operator = new Operator
            {
                Id = operatorId,
                Username = "test_" + Guid.NewGuid().ToString("N")[..8],
                DisplayName = "Test User",
                FirstName = "Test",
                LastName = "User",
                Email = "test@example.com",
                PasswordHash = "hashed",
                Role = OperatorRole.Admin,
                IsApproved = true,
                Active = true
            };

            await _repository.AddAsync(@operator, CancellationToken.None);

            // Act: Increment to 5 of 5 max attempts
            var beforeLockout = DateTime.UtcNow;
            for (int i = 0; i < 5; i++)
            {
                await _repository.IncrementFailedLoginAttemptAsync(operatorId, maxFailedAttempts: 5, lockoutDurationMinutes: 15, CancellationToken.None);
            }
            var afterLockout = DateTime.UtcNow;

            // Assert
            var result = await _repository.GetAsync(operatorId, CancellationToken.None);
            Assert.NotNull(result);
            Assert.Equal(5, result.FailedLoginAttemptCount);
            Assert.NotNull(result.LockedOutUntilUtc);
            Assert.True(result.LockedOutUntilUtc > beforeLockout);
            Assert.True(result.LockedOutUntilUtc < afterLockout.AddMinutes(20));  // Should be ~15 min from now
        }

        [Fact]
        public async Task ResetFailedLoginAttemptsAsync_ClearsBothFields()
        {
            // Arrange: Create an operator with high failed attempts and lockout
            var operatorId = Guid.NewGuid();
            var @operator = new Operator
            {
                Id = operatorId,
                Username = "test_" + Guid.NewGuid().ToString("N")[..8],
                DisplayName = "Test User",
                FirstName = "Test",
                LastName = "User",
                Email = "test@example.com",
                PasswordHash = "hashed",
                Role = OperatorRole.Admin,
                IsApproved = true,
                Active = true,
                FailedLoginAttemptCount = 10,
                LockedOutUntilUtc = DateTime.UtcNow.AddHours(1)
            };

            await using (var db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                db.Operators.Add(@operator);
                await db.SaveChangesAsync(CancellationToken.None);
            }

            // Act
            await _repository.ResetFailedLoginAttemptsAsync(operatorId, CancellationToken.None);

            // Assert
            var result = await _repository.GetAsync(operatorId, CancellationToken.None);
            Assert.NotNull(result);
            Assert.Equal(0, result.FailedLoginAttemptCount);
            Assert.Null(result.LockedOutUntilUtc);
        }

        [Fact]
        public async Task UnlockAsync_ClearsBothFields()
        {
            // Arrange: Create an operator with high failed attempts and lockout
            var operatorId = Guid.NewGuid();
            var @operator = new Operator
            {
                Id = operatorId,
                Username = "test_" + Guid.NewGuid().ToString("N")[..8],
                DisplayName = "Test User",
                FirstName = "Test",
                LastName = "User",
                Email = "test@example.com",
                PasswordHash = "hashed",
                Role = OperatorRole.Admin,
                IsApproved = true,
                Active = true,
                FailedLoginAttemptCount = 10,
                LockedOutUntilUtc = DateTime.UtcNow.AddHours(1)
            };

            await using (var db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                db.Operators.Add(@operator);
                await db.SaveChangesAsync(CancellationToken.None);
            }

            // Act
            await _repository.UnlockAsync(operatorId, CancellationToken.None);

            // Assert
            var result = await _repository.GetAsync(operatorId, CancellationToken.None);
            Assert.NotNull(result);
            Assert.Equal(0, result.FailedLoginAttemptCount);
            Assert.Null(result.LockedOutUntilUtc);
        }

        [Fact]
        public async Task GetByUsernameAsync_FindsOperator()
        {
            // Arrange: Create an operator
            var @operator = new Operator
            {
                Id = Guid.NewGuid(),
                Username = "unique_test_user",
                DisplayName = "Test User",
                FirstName = "Test",
                LastName = "User",
                Email = "test@example.com",
                PasswordHash = "hashed",
                Role = OperatorRole.Admin,
                IsApproved = true,
                Active = true
            };

            await _repository.AddAsync(@operator, CancellationToken.None);

            // Act
            var result = await _repository.GetByUsernameAsync("unique_test_user", CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(@operator.Id, result.Id);
            Assert.Equal("unique_test_user", result.Username);
        }

        [Fact]
        public async Task ReactivateAsync_SetsActiveToTrue()
        {
            // Arrange: Create a deactivated operator
            var operatorId = Guid.NewGuid();
            var @operator = new Operator
            {
                Id = operatorId,
                Username = "test_" + Guid.NewGuid().ToString("N")[..8],
                DisplayName = "Test User",
                FirstName = "Test",
                LastName = "User",
                Email = "test@example.com",
                PasswordHash = "hashed",
                Role = OperatorRole.Admin,
                IsApproved = true,
                Active = false  // Deactivated
            };

            await using (var db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                db.Operators.Add(@operator);
                await db.SaveChangesAsync(CancellationToken.None);
            }

            // Act
            await _repository.ReactivateAsync(operatorId, CancellationToken.None);

            // Assert
            var result = await _repository.GetAsync(operatorId, CancellationToken.None);
            Assert.NotNull(result);
            Assert.True(result.Active);
        }

        [Fact]
        public async Task SetPasswordAsync_UpdatesPasswordAndSecurityStamp()
        {
            // Arrange: Create an operator
            var operatorId = Guid.NewGuid();
            var originalSecurityStamp = Guid.NewGuid();
            var @operator = new Operator
            {
                Id = operatorId,
                Username = "test_" + Guid.NewGuid().ToString("N")[..8],
                DisplayName = "Test User",
                FirstName = "Test",
                LastName = "User",
                Email = "test@example.com",
                PasswordHash = "old_hash",
                Role = OperatorRole.Admin,
                IsApproved = true,
                Active = true,
                SecurityStamp = originalSecurityStamp,
                PasswordUpdatedAtUtc = DateTime.UtcNow.AddHours(-24)
            };

            await _repository.AddAsync(@operator, CancellationToken.None);

            // Act
            string newPasswordHash = "new_secure_hash_12345";
            var beforeUpdate = DateTime.UtcNow;
            await _repository.SetPasswordAsync(operatorId, newPasswordHash, mustChangePassword: true, CancellationToken.None);
            var afterUpdate = DateTime.UtcNow;

            // Assert
            var result = await _repository.GetAsync(operatorId, CancellationToken.None);
            Assert.NotNull(result);
            Assert.Equal(newPasswordHash, result.PasswordHash);
            Assert.True(result.MustChangePassword);
            Assert.NotEqual(originalSecurityStamp, result.SecurityStamp);  // SecurityStamp should change
            Assert.NotNull(result.PasswordUpdatedAtUtc);
            Assert.True(result.PasswordUpdatedAtUtc >= beforeUpdate);
            Assert.True(result.PasswordUpdatedAtUtc <= afterUpdate);
        }

        [Fact]
        public async Task SetRoleAsync_UpdatesRoleOnly()
        {
            // Arrange: Create an operator
            var operatorId = Guid.NewGuid();
            var originalPasswordHash = "hashed_password";
            var originalSecurityStamp = Guid.NewGuid();
            var @operator = new Operator
            {
                Id = operatorId,
                Username = "test_" + Guid.NewGuid().ToString("N")[..8],
                DisplayName = "Test User",
                FirstName = "Test",
                LastName = "User",
                Email = "test@example.com",
                PasswordHash = originalPasswordHash,
                Role = OperatorRole.Admin,
                IsApproved = true,
                Active = true,
                SecurityStamp = originalSecurityStamp
            };

            await _repository.AddAsync(@operator, CancellationToken.None);

            // Act
            await _repository.SetRoleAsync(operatorId, OperatorRole.SuperAdmin, CancellationToken.None);

            // Assert
            var result = await _repository.GetAsync(operatorId, CancellationToken.None);
            Assert.NotNull(result);
            Assert.Equal(OperatorRole.SuperAdmin, result.Role);
            // Other fields should remain unchanged
            Assert.Equal(originalPasswordHash, result.PasswordHash);
            Assert.Equal(originalSecurityStamp, result.SecurityStamp);
        }

        [Fact]
        public async Task SetApprovalFirstLoginNotifiedAsync_SetsNotificationTime()
        {
            // Arrange: Create an operator
            var operatorId = Guid.NewGuid();
            var @operator = new Operator
            {
                Id = operatorId,
                Username = "test_" + Guid.NewGuid().ToString("N")[..8],
                DisplayName = "Test User",
                FirstName = "Test",
                LastName = "User",
                Email = "test@example.com",
                PasswordHash = "hashed",
                Role = OperatorRole.Admin,
                IsApproved = true,
                Active = true,
                ApprovalFirstLoginNotifiedAtUtc = null  // Not yet notified
            };

            await _repository.AddAsync(@operator, CancellationToken.None);

            // Act
            var beforeCall = DateTime.UtcNow;
            await _repository.SetApprovalFirstLoginNotifiedAsync(operatorId, CancellationToken.None);
            var afterCall = DateTime.UtcNow;

            // Assert
            var result = await _repository.GetAsync(operatorId, CancellationToken.None);
            Assert.NotNull(result);
            Assert.NotNull(result.ApprovalFirstLoginNotifiedAtUtc);
            Assert.True(result.ApprovalFirstLoginNotifiedAtUtc >= beforeCall);
            Assert.True(result.ApprovalFirstLoginNotifiedAtUtc <= afterCall);
        }

        [Fact]
        public async Task GetByUsernameAsync_IsCaseSensitive()
        {
            // Arrange: Create an operator with specific username
            var @operator = new Operator
            {
                Id = Guid.NewGuid(),
                Username = "TestUser_CaseSensitive",
                DisplayName = "Test User",
                FirstName = "Test",
                LastName = "User",
                Email = "test@example.com",
                PasswordHash = "hashed",
                Role = OperatorRole.Admin,
                IsApproved = true,
                Active = true
            };

            await _repository.AddAsync(@operator, CancellationToken.None);

            // Act & Assert: Exact match should work
            var found = await _repository.GetByUsernameAsync("TestUser_CaseSensitive", CancellationToken.None);
            Assert.NotNull(found);
            Assert.Equal(@operator.Id, found.Id);

            // Act & Assert: Different case should not match (case-sensitive)
            var notFound = await _repository.GetByUsernameAsync("testuser_casesensitive", CancellationToken.None);
            Assert.Null(notFound);
        }
    }
}
