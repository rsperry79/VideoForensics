using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
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
            var @operator = new Operator
            {
                Id = Guid.NewGuid(),
                DisplayName = "John Forensics",
                CreatedAtUtc = DateTime.UtcNow,
                Active = true
            };

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
            var op1 = new Operator
            {
                Id = Guid.NewGuid(),
                DisplayName = "Alice",
                CreatedAtUtc = DateTime.UtcNow,
                Active = true
            };

            var op2 = new Operator
            {
                Id = Guid.NewGuid(),
                DisplayName = "Bob",
                CreatedAtUtc = DateTime.UtcNow,
                Active = true
            };

            var op3 = new Operator
            {
                Id = Guid.NewGuid(),
                DisplayName = "Charlie",
                CreatedAtUtc = DateTime.UtcNow,
                Active = false
            };

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
            var @operator = new Operator
            {
                Id = Guid.NewGuid(),
                DisplayName = "Test Operator",
                CreatedAtUtc = DateTime.UtcNow,
                Active = true
            };

            _ = await _repository.AddAsync(@operator, CancellationToken.None);

            bool isEmpty = await _repository.IsEmptyAsync(CancellationToken.None);

            Assert.False(isEmpty);
        }

        [Fact]
        public async Task OperatorRepository_DeactivateAsync_SetsFalse()
        {
            var @operator = new Operator
            {
                Id = Guid.NewGuid(),
                DisplayName = "Test Operator",
                CreatedAtUtc = DateTime.UtcNow,
                Active = true
            };

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
            var @operator = new Operator
            {
                Id = Guid.NewGuid(),
                DisplayName = "Active Operator",
                CreatedAtUtc = DateTime.UtcNow,
                Active = true
            };

            Operator result = await _repository.AddAsync(@operator, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(@operator.Id, result.Id);
            Assert.True(result.Active);
        }

        [Fact]
        public async Task OperatorRepository_MultipleDeactivations_Idempotent()
        {
            var @operator = new Operator
            {
                Id = Guid.NewGuid(),
                DisplayName = "Test Operator",
                CreatedAtUtc = DateTime.UtcNow,
                Active = true
            };

            _ = await _repository.AddAsync(@operator, CancellationToken.None);

            await _repository.DeactivateAsync(@operator.Id, CancellationToken.None);
            await _repository.DeactivateAsync(@operator.Id, CancellationToken.None);

            Operator? retrieved = await _repository.GetAsync(@operator.Id, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.False(retrieved.Active);
        }
    }
}
