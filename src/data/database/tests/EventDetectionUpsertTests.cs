using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>Verifies the EventDetection upsert is idempotent: one row per event no matter how many times it runs.</summary>
    public class EventDetectionUpsertTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private UnitOfWork _unitOfWork = null!;
        private ILoggerFactory _loggerFactory = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            _loggerFactory = LoggerFactory.Create(b => { });
            var serviceProvider = new TestServiceProvider(_fixture, _loggerFactory);
            _unitOfWork = new UnitOfWork(_fixture.Factory, serviceProvider, _loggerFactory.CreateLogger<UnitOfWork>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
            _loggerFactory.Dispose();
        }

        private async Task UpsertAsync(Guid eventId, string description, string zoneId)
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                var detection = new EventDetection
                {
                    Id = Guid.NewGuid(),
                    EventId = eventId,
                    DetectionType = "Motion",
                    FullDescription = description
                };

                _ = await context.DetectionEntities.UpsertEventDetectionAsync(detection, CancellationToken.None);

                var zones = new List<EventDetectionZone>
                {
                    new() { Id = Guid.NewGuid(), EventDetectionId = detection.Id, ZoneId = zoneId, ZoneName = zoneId }
                };
                await context.DetectionEntities.AddEventDetectionZonesAsync(zones, CancellationToken.None);
                return null;
            }, CancellationToken.None);
        }

        [Fact]
        public async Task UpsertEventDetectionAsync_CalledTwiceForSameEvent_LeavesOneRow()
        {
            var eventId = Guid.NewGuid();

            await UpsertAsync(eventId, "first", "zone-a");
            await UpsertAsync(eventId, "second", "zone-a");

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            List<EventDetection> rows = await ctx.EventDetections.Where(d => d.EventId == eventId).ToListAsync();
            _ = Assert.Single(rows);
            Assert.Equal("second", rows[0].FullDescription);
        }

        [Fact]
        public async Task UpsertEventDetectionAsync_CalledTwice_ReplacesZonesInsteadOfDuplicating()
        {
            var eventId = Guid.NewGuid();

            await UpsertAsync(eventId, "first", "zone-a");
            await UpsertAsync(eventId, "second", "zone-b");

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            EventDetection detection = await ctx.EventDetections.SingleAsync(d => d.EventId == eventId);
            List<EventDetectionZone> zones = await ctx.EventDetectionZones.Where(z => z.EventDetectionId == detection.Id).ToListAsync();
            EventDetectionZone zone = Assert.Single(zones);
            Assert.Equal("zone-b", zone.ZoneId);
        }

        [Fact]
        public async Task UpsertEventDetectionAsync_DifferentEvents_KeepsSeparateRows()
        {
            var eventA = Guid.NewGuid();
            var eventB = Guid.NewGuid();

            await UpsertAsync(eventA, "a", "zone-a");
            await UpsertAsync(eventB, "b", "zone-a");

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            Assert.Equal(2, await ctx.EventDetections.CountAsync(d => d.EventId == eventA || d.EventId == eventB));
        }

        [Fact]
        public async Task UpsertEventDetectionAsync_TwiceInsideOneTransaction_LeavesOneRow()
        {
            var eventId = Guid.NewGuid();

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                _ = await context.DetectionEntities.UpsertEventDetectionAsync(
                    new EventDetection { Id = Guid.NewGuid(), EventId = eventId, FullDescription = "one" }, CancellationToken.None);
                _ = await context.DetectionEntities.UpsertEventDetectionAsync(
                    new EventDetection { Id = Guid.NewGuid(), EventId = eventId, FullDescription = "two" }, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            _ = Assert.Single(await ctx.EventDetections.Where(d => d.EventId == eventId).ToListAsync());
        }

        [Fact]
        public async Task ClearEventAlertsAndPersonsAsync_ClearsOnlyRequestedKinds()
        {
            var eventId = Guid.NewGuid();
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await context.DetectionEntities.AddEventSecurityAlertsAsync(
                    [new EventSecurityAlert { Id = Guid.NewGuid(), EventId = eventId, Severity = "high", AlertText = "a" }], CancellationToken.None);
                await context.DetectionEntities.AddEventDetectedPersonsAsync(
                    [new EventDetectedPerson { Id = Guid.NewGuid(), EventId = eventId, ProfileId = "p1" }], CancellationToken.None);
                return null;
            }, CancellationToken.None);

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await context.DetectionEntities.ClearEventAlertsAndPersonsAsync(eventId, clearAlerts: true, clearPersons: false, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            Assert.Equal(0, await ctx.EventSecurityAlerts.CountAsync(a => a.EventId == eventId));
            Assert.Equal(1, await ctx.EventDetectedPersons.CountAsync(p => p.EventId == eventId));
        }
    }
}
