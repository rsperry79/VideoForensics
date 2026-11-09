using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;
using VideoForensics.Utils.Testing;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    /// <summary>
    /// The backfill resolves a numeric DownloadEvents id to the string ding id for the device. Only an exact match is
    /// written; unmatched and ambiguous rows are skipped. Non-numeric ids are kept as they are.
    /// </summary>
    public class EventBackfillPrefixMatchTests : IAsyncLifetime
    {
        private const string DoorbotDeviceId = "86401464";
        private SqliteInMemoryFixture _fixture = null!;
        private EventRepository _eventRepository = null!;
        private DownloadEventRepository _downloadEventRepository = null!;
        private MediaItemRepository _mediaItemRepository = null!;
        private UnitOfWork _unitOfWork = null!;
        private ILoggerFactory _loggerFactory = null!;
        private EventBackfillService _backfillService = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            _loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            var serviceProvider = new TestServiceProvider(_fixture, _loggerFactory);
            _unitOfWork = new UnitOfWork(_fixture.Factory, serviceProvider, _loggerFactory.CreateLogger<UnitOfWork>());
            _eventRepository = new EventRepository(_unitOfWork, _loggerFactory.CreateLogger<EventRepository>());
            _mediaItemRepository = new MediaItemRepository(_unitOfWork, _loggerFactory.CreateLogger<MediaItemRepository>());
            _downloadEventRepository = new DownloadEventRepository(_unitOfWork, _loggerFactory.CreateLogger<DownloadEventRepository>());
            _backfillService = new EventBackfillService(new DingIdLinkRepository(_unitOfWork));
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
            _loggerFactory.Dispose();
        }

        [Fact]
        public async Task EventBackfill_ExactDingIdInCandidates_KeepsIdAsIs()
        {
            Device device = await SeedDeviceAsync();
            await SeedLocationEventAsync("1234567890123456789");
            await AddDownloadEventAsync(device.Id, "1234567890123456789");

            int result = await RunBackfillAsync();

            Assert.Equal(1, result);
            Assert.Equal(new[] { "1234567890123456789" }, await EventIdsAsync());
        }

        [Fact]
        public async Task EventBackfill_NumericIdOnlyPrefixOfCandidate_SkipsRow()
        {
            Device device = await SeedDeviceAsync();
            await SeedLocationEventAsync("1234567890123456789");
            await AddDownloadEventAsync(device.Id, "1234567890123456");

            int result = await RunBackfillAsync();

            Assert.Equal(0, result);
            Assert.Empty(await EventIdsAsync());
        }

        [Fact]
        public async Task EventBackfill_NumericIdWithoutCandidate_SkipsRow()
        {
            Device device = await SeedDeviceAsync();
            await AddDownloadEventAsync(device.Id, "1234567890123456789");

            int result = await RunBackfillAsync();

            Assert.Equal(0, result);
            Assert.Empty(await EventIdsAsync());
        }

        [Fact]
        public async Task EventBackfill_NonNumericId_KeepsIdAsIs()
        {
            Device device = await SeedDeviceAsync();
            await AddDownloadEventAsync(device.Id, "snapshot-20261102101010");

            int result = await RunBackfillAsync();

            Assert.Equal(1, result);
            Assert.Equal(new[] { "snapshot-20261102101010" }, await EventIdsAsync());
        }

        private async Task<int> RunBackfillAsync()
        {
            return await _backfillService.BackfillFromDownloadEventsAsync(
                _downloadEventRepository,
                _mediaItemRepository,
                _eventRepository,
                _unitOfWork,
                new Mock<ILogger>().Object,
                CancellationToken.None);
        }

        private async Task<List<string>> EventIdsAsync()
        {
            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            return await db.Events.Select(e => e.ProviderEventId).OrderBy(id => id).ToListAsync();
        }

        private async Task<Device> SeedDeviceAsync()
        {
            Device device = TestDataBuilder.BuildDevice(providerDeviceId: DoorbotDeviceId);
            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            _ = db.Devices.Add(device);
            _ = await db.SaveChangesAsync();
            return device;
        }

        private async Task SeedLocationEventAsync(string dingIdString)
        {
            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            _ = db.RingLocationEvents.Add(new RingLocationEvent
            {
                Id = Guid.NewGuid(),
                ProviderLocationId = "location-1",
                ProviderEventId = dingIdString,
                DoorbotId = long.Parse(DoorbotDeviceId, System.Globalization.CultureInfo.InvariantCulture),
                DingIdString = dingIdString,
            });
            _ = await db.SaveChangesAsync();
        }

        private async Task AddDownloadEventAsync(Guid deviceId, string providerEventId)
        {
            DownloadEvent downloadEvent = TestDataBuilder.BuildDownloadEvent(deviceId, providerEventId, success: true);
            await _downloadEventRepository.AddAsync(downloadEvent, CancellationToken.None);
        }
    }
}
