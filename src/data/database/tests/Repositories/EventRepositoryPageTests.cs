using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Utils.Testing;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    /// <summary>
    /// Tests for the paged, multi-device event query behind the review page's Events tab. Filtering, ordering, paging and
    /// counting must all run in SQL, so the total count is independent of the page size.
    /// </summary>
    public class EventRepositoryPageTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private EventRepository _repository = null!;

        private static readonly DateTime Start = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            _repository = new EventRepository(_fixture.Factory, NullLogger<EventRepository>.Instance);
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        /// <summary>Builds an event whose EventType doubles as a readable label, e.g. "evt-5".</summary>
        private static Event Ev(Guid deviceId, int minute, string eventType = "motion", Guid? id = null)
        {
            return new Event
            {
                Id = id ?? Guid.NewGuid(),
                DeviceId = deviceId,
                ProviderEventId = Guid.NewGuid().ToString("N"),
                EventType = eventType,
                OccurredAtUtc = Start.AddMinutes(minute),
                DiscoveredAtUtc = Start,
            };
        }

        /// <summary>Verifies that events from devices not asked for are left out and the rest come back newest first.</summary>
        [Fact]
        public async Task EventRepository_ListPageByDevicesAndDateRangeAsync_MultipleDevices_ReturnsRequestedDevicesNewestFirst()
        {
            var cameraA = Guid.NewGuid();
            var cameraB = Guid.NewGuid();
            var other = Guid.NewGuid();
            _ = await _repository.CreateAsync(Ev(cameraA, 1, "evt-1"), CancellationToken.None);
            _ = await _repository.CreateAsync(Ev(cameraB, 5, "evt-5"), CancellationToken.None);
            _ = await _repository.CreateAsync(Ev(other, 2, "evt-2"), CancellationToken.None);

            var page = await _repository.ListPageByDevicesAndDateRangeAsync(
                new[] { cameraA, cameraB }, Start, Start.AddHours(1), null, skip: 0, take: 10, CancellationToken.None);

            Assert.Equal(2, page.TotalCount);
            Assert.Equal(new[] { "evt-5", "evt-1" }, page.Items.Select(e => e.EventType).ToArray());
        }

        /// <summary>Verifies that the date range is inclusive at both ends and excludes events just outside it.</summary>
        [Fact]
        public async Task EventRepository_ListPageByDevicesAndDateRangeAsync_DateRange_IsInclusiveAtBothEnds()
        {
            var device = Guid.NewGuid();
            _ = await _repository.CreateAsync(Ev(device, 0, "start"), CancellationToken.None);
            _ = await _repository.CreateAsync(Ev(device, 60, "end"), CancellationToken.None);
            _ = await _repository.CreateAsync(Ev(device, 61, "after"), CancellationToken.None);
            _ = await _repository.CreateAsync(Ev(device, -1, "before"), CancellationToken.None);

            var page = await _repository.ListPageByDevicesAndDateRangeAsync(
                new[] { device }, Start, Start.AddMinutes(60), null, skip: 0, take: 10, CancellationToken.None);

            Assert.Equal(2, page.TotalCount);
            Assert.Equal(new[] { "end", "start" }, page.Items.Select(e => e.EventType).ToArray());
        }

        /// <summary>Verifies that event types in the exclusion list are dropped, and that the count reflects the exclusion.</summary>
        [Fact]
        public async Task EventRepository_ListPageByDevicesAndDateRangeAsync_ExcludeEventTypes_DropsThemFromPageAndTotal()
        {
            var device = Guid.NewGuid();
            _ = await _repository.CreateAsync(Ev(device, 1, "motion"), CancellationToken.None);
            _ = await _repository.CreateAsync(Ev(device, 2, "person"), CancellationToken.None);
            _ = await _repository.CreateAsync(Ev(device, 3, "ring"), CancellationToken.None);

            var page = await _repository.ListPageByDevicesAndDateRangeAsync(
                new[] { device }, Start, Start.AddHours(1), new[] { "person" }, skip: 0, take: 10, CancellationToken.None);

            Assert.Equal(2, page.TotalCount);
            Assert.DoesNotContain(page.Items, e => e.EventType == "person");
        }

        /// <summary>Verifies that events sharing a timestamp come back in the same order on every call, so paging never skips or repeats a row.</summary>
        [Fact]
        public async Task EventRepository_ListPageByDevicesAndDateRangeAsync_TiedTimestamps_OrderIsStableAcrossCalls()
        {
            var device = Guid.NewGuid();
            _ = await _repository.CreateAsync(Ev(device, 4, "tie-a"), CancellationToken.None);
            _ = await _repository.CreateAsync(Ev(device, 4, "tie-b"), CancellationToken.None);
            _ = await _repository.CreateAsync(Ev(device, 9, "newest"), CancellationToken.None);

            var first = await _repository.ListPageByDevicesAndDateRangeAsync(
                new[] { device }, Start, Start.AddHours(1), null, skip: 0, take: 10, CancellationToken.None);
            var second = await _repository.ListPageByDevicesAndDateRangeAsync(
                new[] { device }, Start, Start.AddHours(1), null, skip: 0, take: 10, CancellationToken.None);

            Assert.Equal("newest", first.Items[0].EventType);
            Assert.Equal(first.Items.Select(e => e.Id).ToArray(), second.Items.Select(e => e.Id).ToArray());
        }

        /// <summary>Verifies that the total counts every match while the page holds only the requested slice.</summary>
        [Fact]
        public async Task EventRepository_ListPageByDevicesAndDateRangeAsync_SkipAndTake_TotalIndependentOfPage()
        {
            var device = Guid.NewGuid();
            for (int minute = 0; minute < 7; minute++)
            {
                _ = await _repository.CreateAsync(Ev(device, minute, $"evt-{minute}"), CancellationToken.None);
            }

            var page = await _repository.ListPageByDevicesAndDateRangeAsync(
                new[] { device }, Start, Start.AddHours(1), null, skip: 5, take: 3, CancellationToken.None);

            Assert.Equal(7, page.TotalCount);
            Assert.Equal(new[] { "evt-1", "evt-0" }, page.Items.Select(e => e.EventType).ToArray());
        }

        /// <summary>Verifies that take outside 1..500 is rejected before any query runs.</summary>
        [Theory]
        [InlineData(0)]
        [InlineData(501)]
        public async Task EventRepository_ListPageByDevicesAndDateRangeAsync_TakeOutOfRange_ThrowsArgumentOutOfRange(int take)
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                _repository.ListPageByDevicesAndDateRangeAsync(
                    new[] { Guid.NewGuid() }, Start, Start.AddHours(1), null, skip: 0, take: take, CancellationToken.None));
        }

        /// <summary>Verifies that a negative skip is rejected.</summary>
        [Fact]
        public async Task EventRepository_ListPageByDevicesAndDateRangeAsync_NegativeSkip_ThrowsArgumentOutOfRange()
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                _repository.ListPageByDevicesAndDateRangeAsync(
                    new[] { Guid.NewGuid() }, Start, Start.AddHours(1), null, skip: -1, take: 10, CancellationToken.None));
        }

        /// <summary>Verifies that the device list must hold 1..64 ids, keeping the SQL IN list bounded.</summary>
        [Fact]
        public async Task EventRepository_ListPageByDevicesAndDateRangeAsync_DeviceCountOutOfRange_ThrowsArgumentOutOfRange()
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                _repository.ListPageByDevicesAndDateRangeAsync(
                    Array.Empty<Guid>(), Start, Start.AddHours(1), null, skip: 0, take: 10, CancellationToken.None));

            Guid[] tooMany = Enumerable.Range(0, 65).Select(_ => Guid.NewGuid()).ToArray();
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                _repository.ListPageByDevicesAndDateRangeAsync(
                    tooMany, Start, Start.AddHours(1), null, skip: 0, take: 10, CancellationToken.None));
        }
    }
}
