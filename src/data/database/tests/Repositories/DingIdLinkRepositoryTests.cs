using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;
using VideoForensics.Utils.Testing;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    /// <summary>
    /// The download path matches a doorbot history id to the string ding ids of its own device, then links the doorbot
    /// row to the Events row it creates. Both reads and writes go through the unit of work context.
    /// </summary>
    public class DingIdLinkRepositoryTests : IAsyncLifetime
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
        public async Task DingIdLinks_CandidatesForDevice_ReturnsOnlyThatDevicesStringDingIds()
        {
            _ = await SeedDeviceAsync(DoorbotId.ToString());
            await SeedLocationEventAsync(DoorbotId, "1234567890123456789");
            await SeedLocationEventAsync(999, "9999999999999999999");

            IReadOnlyList<string> candidates = await _unitOfWork.ExecuteAsync(
                ctx => ctx.DingIdLinks.GetCandidateDingIdsAsync(DoorbotId, CancellationToken.None), CancellationToken.None);

            Assert.Equal(new[] { "1234567890123456789" }, candidates);
        }

        [Fact]
        public async Task DingIdLinks_GetLinkedEvent_ReturnsTheRowTheDoorbotRowLinksTo()
        {
            Device device = await SeedDeviceAsync(DoorbotId.ToString());
            var evt = new Event { Id = Guid.NewGuid(), DeviceId = device.Id, ProviderEventId = DoorbotId.ToString(), EventType = "motion", OccurredAtUtc = DateTime.UtcNow, DiscoveredAtUtc = DateTime.UtcNow };
            await using (VideoForensicsDbContext seed = _fixture.Factory.CreateDbContext())
            {
                _ = seed.Events.Add(evt);
                _ = seed.RingDoorbotHistoryEvents.Add(new RingDoorbotHistoryEvent { Id = Guid.NewGuid(), ProviderEventId = "1234567890123456", LinkedEventId = evt.Id });
                _ = await seed.SaveChangesAsync();
            }

            Event? linked = await _unitOfWork.ExecuteAsync(
                ctx => ctx.DingIdLinks.GetLinkedEventAsync("1234567890123456", CancellationToken.None), CancellationToken.None);

            Assert.Equal(evt.Id, linked?.Id);
        }

        [Fact]
        public async Task DingIdLinks_RekeyLinkedEvent_ChangesItsProviderEventIdToTheDingId()
        {
            Device device = await SeedDeviceAsync(DoorbotId.ToString());
            var evt = new Event { Id = Guid.NewGuid(), DeviceId = device.Id, ProviderEventId = DoorbotId.ToString(), EventType = "motion", OccurredAtUtc = DateTime.UtcNow, DiscoveredAtUtc = DateTime.UtcNow };
            await using (VideoForensicsDbContext seed = _fixture.Factory.CreateDbContext())
            {
                _ = seed.Events.Add(evt);
                _ = await seed.SaveChangesAsync();
            }

            _ = await _unitOfWork.ExecuteAsync(async ctx =>
            {
                await ctx.DingIdLinks.RekeyLinkedEventAsync(evt.Id, "1234567890123456789", CancellationToken.None);
                return true;
            }, CancellationToken.None);

            await using VideoForensicsDbContext check = _fixture.Factory.CreateDbContext();
            Assert.Equal("1234567890123456789", (await check.Events.FindAsync(evt.Id))!.ProviderEventId);
        }

        [Fact]
        public async Task DingIdLinks_NoDeviceForDoorbot_ReturnsNoCandidates()
        {
            // Device-first: a location row with a matching doorbot id does not count without a device to scope it.
            await SeedLocationEventAsync(DoorbotId, "1234567890123456789");

            IReadOnlyList<string> candidates = await _unitOfWork.ExecuteAsync(
                ctx => ctx.DingIdLinks.GetCandidateDingIdsAsync(DoorbotId, CancellationToken.None), CancellationToken.None);

            Assert.Empty(candidates);
        }

        [Fact]
        public async Task DingIdLinks_LinkDoorbotRow_SetsLinkedEventId()
        {
            Device device = await SeedDeviceAsync(DoorbotId.ToString());
            var evt = new Event { Id = Guid.NewGuid(), DeviceId = device.Id, ProviderEventId = "1234567890123456789", EventType = "motion", OccurredAtUtc = DateTime.UtcNow, DiscoveredAtUtc = DateTime.UtcNow };
            await using (VideoForensicsDbContext seed = _fixture.Factory.CreateDbContext())
            {
                _ = seed.Events.Add(evt);
                _ = seed.RingDoorbotHistoryEvents.Add(new RingDoorbotHistoryEvent { Id = Guid.NewGuid(), ProviderEventId = "1234567890123456" });
                _ = await seed.SaveChangesAsync();
            }

            _ = await _unitOfWork.ExecuteAsync(async ctx =>
            {
                await ctx.DingIdLinks.LinkDoorbotHistoryEventAsync("1234567890123456", evt.Id, CancellationToken.None);
                return true;
            }, CancellationToken.None);

            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            Assert.Equal(evt.Id, (await db.RingDoorbotHistoryEvents.SingleAsync()).LinkedEventId);
        }

        private async Task<Device> SeedDeviceAsync(string providerDeviceId)
        {
            Device device = TestDataBuilder.BuildDevice(providerDeviceId: providerDeviceId);
            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            _ = db.Devices.Add(device);
            _ = await db.SaveChangesAsync();
            return device;
        }

        private async Task SeedLocationEventAsync(long doorbotId, string dingIdString)
        {
            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            _ = db.RingLocationEvents.Add(new RingLocationEvent
            {
                Id = Guid.NewGuid(),
                ProviderLocationId = "location-1",
                ProviderEventId = dingIdString,
                DoorbotId = doorbotId,
                DingIdString = dingIdString,
            });
            _ = await db.SaveChangesAsync();
        }
    }
}
