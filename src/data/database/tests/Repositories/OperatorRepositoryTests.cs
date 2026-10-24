using Microsoft.Extensions.Logging;

using VideoForensics.Utils.Testing;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    public class OperatorRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private OperatorRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            _repository = new OperatorRepository(_fixture.Factory, loggerFactory.CreateLogger<OperatorRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task OperatorRepository_AddAndGet_RoundTrips()
        {
            Operator @operator = TestDataBuilder.BuildOperator(displayName: "John Forensics");

            _ = await _repository.AddAsync(@operator, CancellationToken.None);
            Operator? retrieved = await _repository.GetAsync(@operator.Id, CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal(@operator.Id, retrieved.Id);
            Assert.Equal("John Forensics", retrieved.DisplayName);
            Assert.Equal(@operator.Active, retrieved.Active);
        }

        [Fact]
        public async Task OperatorRepository_Get_NotFound_ReturnsNull()
        {
            var nonexistentId = Guid.NewGuid();
            Operator? retrieved = await _repository.GetAsync(nonexistentId, CancellationToken.None);

            Assert.Null(retrieved);
        }

        [Fact]
        public async Task OperatorRepository_ListAsync_ReturnsAllOperators()
        {
            Operator op1 = TestDataBuilder.BuildOperator(displayName: "Alice");
            Operator op2 = TestDataBuilder.BuildOperator(displayName: "Bob");
            Operator op3 = TestDataBuilder.BuildOperator(displayName: "Charlie", active: false);

            _ = await _repository.AddAsync(op1, CancellationToken.None);
            _ = await _repository.AddAsync(op2, CancellationToken.None);
            _ = await _repository.AddAsync(op3, CancellationToken.None);

            IReadOnlyList<Operator> list = await _repository.ListAsync(CancellationToken.None);

            Assert.Equal(3, list.Count);
            // Should be ordered by DisplayName
            Assert.Equal("Alice", list[0].DisplayName);
            Assert.Equal("Bob", list[1].DisplayName);
            Assert.Equal("Charlie", list[2].DisplayName);
        }

        [Fact]
        public async Task OperatorRepository_ListAsync_Empty_ReturnsEmptyList()
        {
            IReadOnlyList<Operator> list = await _repository.ListAsync(CancellationToken.None);

            Assert.Empty(list);
        }

        [Fact]
        public async Task OperatorRepository_IsEmptyAsync_WhenEmpty_ReturnsTrue()
        {
            bool isEmpty = await _repository.IsEmptyAsync(CancellationToken.None);

            Assert.True(isEmpty);
        }

        [Fact]
        public async Task OperatorRepository_IsEmptyAsync_WithOperators_ReturnsFalse()
        {
            Operator @operator = TestDataBuilder.BuildOperator();

            _ = await _repository.AddAsync(@operator, CancellationToken.None);

            bool isEmpty = await _repository.IsEmptyAsync(CancellationToken.None);

            Assert.False(isEmpty);
        }

        [Fact]
        public async Task OperatorRepository_DeactivateAsync_SetsFalse()
        {
            Operator @operator = TestDataBuilder.BuildOperator();

            _ = await _repository.AddAsync(@operator, CancellationToken.None);

            await _repository.DeactivateAsync(@operator.Id, CancellationToken.None);

            Operator? retrieved = await _repository.GetAsync(@operator.Id, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.False(retrieved.Active);
        }

        [Fact]
        public async Task OperatorRepository_DeactivateAsync_NonexistentId_NoThrow()
        {
            var nonexistentId = Guid.NewGuid();

            // Should not throw
            await _repository.DeactivateAsync(nonexistentId, CancellationToken.None);

            Operator? retrieved = await _repository.GetAsync(nonexistentId, CancellationToken.None);
            Assert.Null(retrieved);
        }

        [Fact]
        public async Task OperatorRepository_Add_AlreadyActive_Persists()
        {
            Operator @operator = TestDataBuilder.BuildOperator(displayName: "Active Operator");

            Operator result = await _repository.AddAsync(@operator, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(@operator.Id, result.Id);
            Assert.True(result.Active);
        }

        [Fact]
        public async Task OperatorRepository_MultipleDeactivations_Idempotent()
        {
            Operator @operator = TestDataBuilder.BuildOperator();

            _ = await _repository.AddAsync(@operator, CancellationToken.None);

            await _repository.DeactivateAsync(@operator.Id, CancellationToken.None);
            await _repository.DeactivateAsync(@operator.Id, CancellationToken.None);

            Operator? retrieved = await _repository.GetAsync(@operator.Id, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.False(retrieved.Active);
        }

        [Fact]
        public async Task AddAsync_SanitizesLogOutput_WhenDisplayNameContainsNewlines()
        {
            // Arrange: Create an operator with DisplayName containing CRLF (log injection attempt)
            string injectedDisplayName = "Alice Johnson\r\nFAKE LOG ENTRY: SuperAdmin account created";
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
            _ = await _repository.AddAsync(@operator, CancellationToken.None);

            // Assert: Verify the operator was persisted with the raw DisplayName intact
            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync(CancellationToken.None);
            Operator? persistedOperator = await db.Operators.FindAsync(new object[] { @operator.Id }, cancellationToken: CancellationToken.None);
            Assert.NotNull(persistedOperator);
            // The raw DisplayName (with newlines) should be stored in the database
            Assert.Equal(injectedDisplayName, persistedOperator.DisplayName);
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

            _ = await _repository.AddAsync(@operator, CancellationToken.None);

            // Act: Increment to 4 of 5 max attempts
            await _repository.IncrementFailedLoginAttemptAsync(operatorId, maxFailedAttempts: 5, lockoutDurationMinutes: 15, CancellationToken.None);
            await _repository.IncrementFailedLoginAttemptAsync(operatorId, maxFailedAttempts: 5, lockoutDurationMinutes: 15, CancellationToken.None);
            await _repository.IncrementFailedLoginAttemptAsync(operatorId, maxFailedAttempts: 5, lockoutDurationMinutes: 15, CancellationToken.None);
            await _repository.IncrementFailedLoginAttemptAsync(operatorId, maxFailedAttempts: 5, lockoutDurationMinutes: 15, CancellationToken.None);

            // Assert
            Operator? result = await _repository.GetAsync(operatorId, CancellationToken.None);
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

            _ = await _repository.AddAsync(@operator, CancellationToken.None);

            // Act: Increment to 5 of 5 max attempts
            DateTime beforeLockout = DateTime.UtcNow;
            for (int i = 0; i < 5; i++)
            {
                await _repository.IncrementFailedLoginAttemptAsync(operatorId, maxFailedAttempts: 5, lockoutDurationMinutes: 15, CancellationToken.None);
            }

            DateTime afterLockout = DateTime.UtcNow;

            // Assert
            Operator? result = await _repository.GetAsync(operatorId, CancellationToken.None);
            Assert.NotNull(result);
            Assert.Equal(5, result.FailedLoginAttemptCount);
            _ = Assert.NotNull(result.LockedOutUntilUtc);
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

            await using (VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                _ = db.Operators.Add(@operator);
                _ = await db.SaveChangesAsync(CancellationToken.None);
            }

            // Act
            await _repository.ResetFailedLoginAttemptsAsync(operatorId, CancellationToken.None);

            // Assert
            Operator? result = await _repository.GetAsync(operatorId, CancellationToken.None);
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

            await using (VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                _ = db.Operators.Add(@operator);
                _ = await db.SaveChangesAsync(CancellationToken.None);
            }

            // Act
            await _repository.UnlockAsync(operatorId, CancellationToken.None);

            // Assert
            Operator? result = await _repository.GetAsync(operatorId, CancellationToken.None);
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

            _ = await _repository.AddAsync(@operator, CancellationToken.None);

            // Act
            Operator? result = await _repository.GetByUsernameAsync("unique_test_user", CancellationToken.None);

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

            await using (VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                _ = db.Operators.Add(@operator);
                _ = await db.SaveChangesAsync(CancellationToken.None);
            }

            // Act
            await _repository.ReactivateAsync(operatorId, CancellationToken.None);

            // Assert
            Operator? result = await _repository.GetAsync(operatorId, CancellationToken.None);
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

            _ = await _repository.AddAsync(@operator, CancellationToken.None);

            // Act
            string newPasswordHash = "new_secure_hash_12345";
            DateTime beforeUpdate = DateTime.UtcNow;
            await _repository.SetPasswordAsync(operatorId, newPasswordHash, mustChangePassword: true, CancellationToken.None);
            DateTime afterUpdate = DateTime.UtcNow;

            // Assert
            Operator? result = await _repository.GetAsync(operatorId, CancellationToken.None);
            Assert.NotNull(result);
            Assert.Equal(newPasswordHash, result.PasswordHash);
            Assert.True(result.MustChangePassword);
            Assert.NotEqual(originalSecurityStamp, result.SecurityStamp);  // SecurityStamp should change
            _ = Assert.NotNull(result.PasswordUpdatedAtUtc);
            Assert.True(result.PasswordUpdatedAtUtc >= beforeUpdate);
            Assert.True(result.PasswordUpdatedAtUtc <= afterUpdate);
        }

        [Fact]
        public async Task SetRoleAsync_UpdatesRoleOnly()
        {
            // Arrange: Create an operator
            var operatorId = Guid.NewGuid();
            string originalPasswordHash = "hashed_password";
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

            _ = await _repository.AddAsync(@operator, CancellationToken.None);

            // Act
            await _repository.SetRoleAsync(operatorId, OperatorRole.SuperAdmin, CancellationToken.None);

            // Assert
            Operator? result = await _repository.GetAsync(operatorId, CancellationToken.None);
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

            _ = await _repository.AddAsync(@operator, CancellationToken.None);

            // Act
            DateTime beforeCall = DateTime.UtcNow;
            await _repository.SetApprovalFirstLoginNotifiedAsync(operatorId, CancellationToken.None);
            DateTime afterCall = DateTime.UtcNow;

            // Assert
            Operator? result = await _repository.GetAsync(operatorId, CancellationToken.None);
            Assert.NotNull(result);
            _ = Assert.NotNull(result.ApprovalFirstLoginNotifiedAtUtc);
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

            _ = await _repository.AddAsync(@operator, CancellationToken.None);

            // Act & Assert: Exact match should work
            Operator? found = await _repository.GetByUsernameAsync("TestUser_CaseSensitive", CancellationToken.None);
            Assert.NotNull(found);
            Assert.Equal(@operator.Id, found.Id);

            // Act & Assert: Different case should not match (case-sensitive)
            Operator? notFound = await _repository.GetByUsernameAsync("testuser_casesensitive", CancellationToken.None);
            Assert.Null(notFound);
        }
    }
}
