using Microsoft.Extensions.Logging;

using VideoForensics.Data.Database.Repositories;
using VideoForensics.Utils.Testing;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    /// <summary>
    /// A read through the unit of work must not wait behind a write transaction. Writes hold the process-wide append gate
    /// for their whole transaction, so a read that took the gate would stall on every slow write.
    /// </summary>
    public class UnitOfWorkReadPathTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private UnitOfWork _unitOfWork = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            var serviceProvider = new TestServiceProvider(_fixture, loggerFactory);
            _unitOfWork = new UnitOfWork(_fixture.Factory, serviceProvider, loggerFactory.CreateLogger<UnitOfWork>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task ReadAsync_WhileAWriteTransactionIsOpen_CompletesWithoutWaitingForIt()
        {
            var release = new TaskCompletionSource();
            Task<bool> writer = _unitOfWork.ExecuteAsync(async ctx =>
            {
                await release.Task;
                return true;
            }, CancellationToken.None);

            IReadOnlyList<string> candidates = await _unitOfWork.ReadAsync(
                _ => new DingIdLinkRepository(_unitOfWork).GetCandidateDingIdsAsync(86401464, CancellationToken.None), CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Empty(candidates);
            release.SetResult();
            _ = await writer;
        }
    }
}
