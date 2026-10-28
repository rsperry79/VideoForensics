using Microsoft.Extensions.Logging;

using VideoForensics.Utils.Testing;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    public class UnitOfWorkActionLogChainTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private UnitOfWork _unitOfWork = null!;
        private ActionLogRepository _verifier = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();

            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            var serviceProvider = new TestServiceProvider(_fixture, loggerFactory);

            _unitOfWork = new UnitOfWork(_fixture.Factory, serviceProvider, loggerFactory.CreateLogger<UnitOfWork>());
            _verifier = new ActionLogRepository(_fixture.Factory, loggerFactory.CreateLogger<ActionLogRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task UnitOfWorkActionLogChain_TwoAppendsInOneUnitOfWork_ChainVerifies()
        {
            // Auto-fix appends several entries before the unit of work saves. Each append must chain to the
            // entry appended just before it, not to the last committed entry.
            _ = await _unitOfWork.ExecuteAsync(async context =>
            {
                _ = await context.ActionLog.AppendAsync("System", ActorType.System, "AutoFixNewEvent", nameof(Event), Guid.NewGuid(), "{}", CancellationToken.None);
                _ = await context.ActionLog.AppendAsync("System", ActorType.System, "AutoFixMetadata", nameof(Event), Guid.NewGuid(), "{}", CancellationToken.None);
                _ = await context.ActionLog.AppendAsync("System", ActorType.System, "AutoFixMetadata", nameof(Event), Guid.NewGuid(), "{}", CancellationToken.None);
                return true;
            }, CancellationToken.None);

            bool isValid = await _verifier.VerifyChainIntegrityAsync(CancellationToken.None);

            Assert.True(isValid);
        }

        [Fact]
        public async Task UnitOfWorkActionLogChain_AppendAfterCommittedWork_ChainVerifies()
        {
            _ = await _unitOfWork.ExecuteAsync(async context =>
            {
                _ = await context.ActionLog.AppendAsync("System", ActorType.System, "First", nameof(Event), null, null, CancellationToken.None);
                return true;
            }, CancellationToken.None);

            _ = await _unitOfWork.ExecuteAsync(async context =>
            {
                _ = await context.ActionLog.AppendAsync("System", ActorType.System, "Second", nameof(Event), null, null, CancellationToken.None);
                return true;
            }, CancellationToken.None);

            bool isValid = await _verifier.VerifyChainIntegrityAsync(CancellationToken.None);

            Assert.True(isValid);
        }
    }
}
