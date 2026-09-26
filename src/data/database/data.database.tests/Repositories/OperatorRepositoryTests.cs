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
    }
}
