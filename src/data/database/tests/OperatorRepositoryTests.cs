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
            var @operator = TestDataBuilder.BuildOperator(displayName: "John Forensics");

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
            var op1 = TestDataBuilder.BuildOperator(displayName: "Alice");
            var op2 = TestDataBuilder.BuildOperator(displayName: "Bob");
            var op3 = TestDataBuilder.BuildOperator(displayName: "Charlie", active: false);

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
            var @operator = TestDataBuilder.BuildOperator();

            _ = await _repository.AddAsync(@operator, CancellationToken.None);

            bool isEmpty = await _repository.IsEmptyAsync(CancellationToken.None);

            Assert.False(isEmpty);
        }

        [Fact]
        public async Task OperatorRepository_DeactivateAsync_SetsFalse()
        {
            var @operator = TestDataBuilder.BuildOperator();

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
            var @operator = TestDataBuilder.BuildOperator(displayName: "Active Operator");

            Operator result = await _repository.AddAsync(@operator, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(@operator.Id, result.Id);
            Assert.True(result.Active);
        }

        [Fact]
        public async Task OperatorRepository_MultipleDeactivations_Idempotent()
        {
            var @operator = TestDataBuilder.BuildOperator();

            _ = await _repository.AddAsync(@operator, CancellationToken.None);

            await _repository.DeactivateAsync(@operator.Id, CancellationToken.None);
            await _repository.DeactivateAsync(@operator.Id, CancellationToken.None);

            Operator? retrieved = await _repository.GetAsync(@operator.Id, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.False(retrieved.Active);
        }
    }
}
