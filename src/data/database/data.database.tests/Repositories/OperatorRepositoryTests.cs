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
    }
}
