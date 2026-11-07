using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;
using VideoForensics.Utils.Testing;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    /// <summary>
    /// Candidate ding ids from video search rows: only rows linked to the doorbot, with a ding id, count.
    /// The query starts from the doorbot's links so it does not scan every video search row.
    /// </summary>
    public class DingIdCandidatesVideoSearchTests : IAsyncLifetime
    {
        private const long DoorbotId = 86401464;
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
        public async Task DingIdCandidates_VideoSearchRows_ReturnOnlyLinkedRowsWithADingId()
        {
            await using (VideoForensicsDbContext db = _fixture.Factory.CreateDbContext())
            {
                Device device = TestDataBuilder.BuildDevice(providerDeviceId: DoorbotId.ToString());
                _ = db.Devices.Add(device);

                Guid accountId = Guid.NewGuid();
                var linked = new RingVideoSearchEvent { Id = Guid.NewGuid(), RingAccountId = accountId, ProviderEventId = "111" };
                var otherDoorbot = new RingVideoSearchEvent { Id = Guid.NewGuid(), RingAccountId = accountId, ProviderEventId = "222" };
                var noDingId = new RingVideoSearchEvent { Id = Guid.NewGuid(), RingAccountId = accountId, ProviderEventId = null };
                db.RingVideoSearchEvents.AddRange(linked, otherDoorbot, noDingId);

                _ = db.RingEventDoorbots.Add(new RingEventDoorbot { Id = Guid.NewGuid(), Source = RingEventSources.VideoSearch, SourceRowId = linked.Id, DoorbotId = DoorbotId });
                _ = db.RingEventDoorbots.Add(new RingEventDoorbot { Id = Guid.NewGuid(), Source = RingEventSources.VideoSearch, SourceRowId = otherDoorbot.Id, DoorbotId = 999 });
                _ = db.RingEventDoorbots.Add(new RingEventDoorbot { Id = Guid.NewGuid(), Source = RingEventSources.VideoSearch, SourceRowId = noDingId.Id, DoorbotId = DoorbotId });
                _ = await db.SaveChangesAsync();
            }

            IReadOnlyList<string> candidates = await _unitOfWork.ReadAsync(
                ctx => ctx.DingIdLinks.GetCandidateDingIdsAsync(DoorbotId, CancellationToken.None), CancellationToken.None);

            Assert.Equal(new[] { "111" }, candidates);
        }
    }
}
