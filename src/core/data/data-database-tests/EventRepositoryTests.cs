using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Xunit;
using VideoForensics.Data.Database.Repositories;

namespace VideoForensics.Data.Database.Tests
{
    public class EventRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private EventRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            _repository = new EventRepository(_fixture.Factory, loggerFactory.CreateLogger<EventRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task EventRepository_UpsertAsync_CreatesNewEvent()
        {
            var deviceId = Guid.NewGuid();
            var @event = TestDataBuilder.BuildEvent(deviceId, "evt_001");

            await _repository.UpsertAsync(@event, CancellationToken.None);
            var retrieved = await _repository.GetAsync(@event.Id, CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal(deviceId, retrieved.DeviceId);
            Assert.Equal("evt_001", retrieved.ProviderEventId);
        }

        [Fact]
        public async Task EventRepository_UpsertAsync_UpdatesExistingEvent()
        {
            var deviceId = Guid.NewGuid();
            var @event = TestDataBuilder.BuildEvent(deviceId, "evt_002");

            var created = await _repository.UpsertAsync(@event, CancellationToken.None);
            var createdId = created.Id;

            @event.EventType = "Person";
            var updated = await _repository.UpsertAsync(@event, CancellationToken.None);

            Assert.Equal(createdId, updated.Id);
            Assert.Equal("Person", updated.EventType);
        }

        [Fact]
        public async Task EventRepository_GetByProviderEventId_FindsEvent()
        {
            var deviceId = Guid.NewGuid();
            var @event = TestDataBuilder.BuildEvent(deviceId, "evt_003");

            await _repository.UpsertAsync(@event, CancellationToken.None);
            var retrieved = await _repository.GetByProviderEventIdAsync(deviceId, "evt_003", CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal(@event.Id, retrieved.Id);
        }

        [Fact]
        public async Task EventRepository_ListByDeviceAndDateRange_FiltersCorrectly()
        {
            var deviceId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            var evt1 = TestDataBuilder.BuildEvent(deviceId, "evt_1");
            evt1.OccurredAtUtc = now.AddHours(-2);

            var evt2 = TestDataBuilder.BuildEvent(deviceId, "evt_2");
            evt2.OccurredAtUtc = now;

            var evt3 = TestDataBuilder.BuildEvent(deviceId, "evt_3");
            evt3.OccurredAtUtc = now.AddHours(2);

            await _repository.UpsertAsync(evt1, CancellationToken.None);
            await _repository.UpsertAsync(evt2, CancellationToken.None);
            await _repository.UpsertAsync(evt3, CancellationToken.None);

            var list = await _repository.ListByDeviceAndDateRangeAsync(
                deviceId,
                now.AddHours(-1),
                now.AddHours(1),
                CancellationToken.None);

            Assert.Single(list);
            Assert.Equal(evt2.Id, list[0].Id);
        }

        [Fact]
        public async Task EventRepository_ListAsync_ReturnsAll()
        {
            var evt1 = TestDataBuilder.BuildEvent();
            var evt2 = TestDataBuilder.BuildEvent();

            await _repository.UpsertAsync(evt1, CancellationToken.None);
            await _repository.UpsertAsync(evt2, CancellationToken.None);

            var list = await _repository.ListAsync(CancellationToken.None);
            Assert.Equal(2, list.Count);
        }

        [Fact]
        public async Task EventRepository_DeleteAsync_RemovesEvent()
        {
            var @event = TestDataBuilder.BuildEvent();
            await _repository.UpsertAsync(@event, CancellationToken.None);

            await _repository.DeleteAsync(@event.Id, CancellationToken.None);

            var retrieved = await _repository.GetAsync(@event.Id, CancellationToken.None);
            Assert.Null(retrieved);
        }

        [Fact]
        public async Task EventRepository_UniqueConstraint_DuplicateDeviceEventIdComboThrows()
        {
            var deviceId = Guid.NewGuid();
            var evt1 = TestDataBuilder.BuildEvent(deviceId, "dup_evt");
            var evt2 = TestDataBuilder.BuildEvent(deviceId, "dup_evt");

            await _repository.UpsertAsync(evt1, CancellationToken.None);

            var ctx = _fixture.Factory.CreateDbContext();
            ctx.Events.Add(evt2);

            await Assert.ThrowsAsync<DbUpdateException>(async () =>
                await ctx.SaveChangesAsync());
        }
    }
}
