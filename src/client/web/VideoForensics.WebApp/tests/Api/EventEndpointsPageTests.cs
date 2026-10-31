using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

using Moq;

using VideoForensics.Client.WebApp.Api;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Api
{
    /// <summary>
    /// Tests for the paged events endpoint and the batched event-detail endpoint: validation at the API boundary and the DTOs they return.
    /// </summary>
    public class EventEndpointsPageTests
    {
        private static readonly DateTime From = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime To = From.AddHours(1);

        /// <summary>Verifies that a valid request asks the repository for the requested slice and returns the mapped page with its total.</summary>
        [Fact]
        public async Task ListEventPageAsync_ValidRequest_ReturnsPageWithTotalCount()
        {
            var deviceId = Guid.NewGuid();
            var evt = new Event { Id = Guid.NewGuid(), DeviceId = deviceId, ProviderEventId = "p1", EventType = "motion", OccurredAtUtc = From, DiscoveredAtUtc = From };
            var repository = new Mock<IEventRepository>();
            _ = repository
                .Setup(r => r.ListPageByDevicesAndDateRangeAsync(It.IsAny<IReadOnlyCollection<Guid>>(), From, To, It.IsAny<IReadOnlyCollection<string>?>(), It.IsAny<IReadOnlyCollection<string>?>(), 10, 25, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new EventPage(new List<Event> { evt }, 42));

            IResult result = await EventEndpoints.ListEventPageAsync(
                new[] { deviceId }, From, To, excludeTypes: null, includeTypes: null, skip: 10, take: 25, repository.Object, CancellationToken.None);

            Ok<EventPageDto> ok = Assert.IsType<Ok<EventPageDto>>(result);
            Assert.Equal(42, ok.Value!.TotalCount);
            Assert.Equal(evt.Id, Assert.Single(ok.Value.Items).Id);
        }

        /// <summary>Verifies that omitting skip and take uses the default page, from zero with 100 items.</summary>
        [Fact]
        public async Task ListEventPageAsync_NoPaging_UsesDefaultPage()
        {
            var repository = new Mock<IEventRepository>();
            _ = repository
                .Setup(r => r.ListPageByDevicesAndDateRangeAsync(It.IsAny<IReadOnlyCollection<Guid>>(), From, To, It.IsAny<IReadOnlyCollection<string>?>(), It.IsAny<IReadOnlyCollection<string>?>(), 0, 100, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new EventPage(new List<Event>(), 0));

            IResult result = await EventEndpoints.ListEventPageAsync(
                new[] { Guid.NewGuid() }, From, To, excludeTypes: null, includeTypes: null, skip: null, take: null, repository.Object, CancellationToken.None);

            _ = Assert.IsType<Ok<EventPageDto>>(result);
            repository.Verify(r => r.ListPageByDevicesAndDateRangeAsync(It.IsAny<IReadOnlyCollection<Guid>>(), From, To, It.IsAny<IReadOnlyCollection<string>?>(), It.IsAny<IReadOnlyCollection<string>?>(), 0, 100, It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>Verifies that excludeTypes is forwarded to the repository.</summary>
        [Fact]
        public async Task ListEventPageAsync_ExcludeTypes_ForwardsToRepository()
        {
            var device = Guid.NewGuid();
            var repository = new Mock<IEventRepository>();
            _ = repository
                .Setup(r => r.ListPageByDevicesAndDateRangeAsync(It.IsAny<IReadOnlyCollection<Guid>>(), From, To, It.Is<IReadOnlyCollection<string>?>(x => x != null && x.Contains("person")), It.IsAny<IReadOnlyCollection<string>?>(), 0, 10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new EventPage(new List<Event>(), 0));

            IResult result = await EventEndpoints.ListEventPageAsync(
                new[] { device }, From, To, excludeTypes: new[] { "person" }, includeTypes: null, skip: 0, take: 10, repository.Object, CancellationToken.None);

            _ = Assert.IsType<Ok<EventPageDto>>(result);
            repository.VerifyAll();
        }

        /// <summary>Verifies that includeTypes is forwarded to the repository.</summary>
        [Fact]
        public async Task ListEventPageAsync_IncludeTypes_ForwardsToRepository()
        {
            var device = Guid.NewGuid();
            var repository = new Mock<IEventRepository>();
            _ = repository
                .Setup(r => r.ListPageByDevicesAndDateRangeAsync(It.IsAny<IReadOnlyCollection<Guid>>(), From, To, It.IsAny<IReadOnlyCollection<string>?>(), It.Is<IReadOnlyCollection<string>?>(x => x != null && x.Count == 2 && x.Contains("person") && x.Contains("ring")), 0, 10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new EventPage(new List<Event>(), 0));

            IResult result = await EventEndpoints.ListEventPageAsync(
                new[] { device }, From, To, excludeTypes: null, includeTypes: new[] { "person", "ring" }, skip: 0, take: 10, repository.Object, CancellationToken.None);

            _ = Assert.IsType<Ok<EventPageDto>>(result);
            repository.VerifyAll();
        }

        /// <summary>Verifies that a request with no device IDs is rejected before any query runs.</summary>
        [Fact]
        public async Task ListEventPageAsync_NoDevices_ReturnsBadRequest()
        {
            var repository = new Mock<IEventRepository>();

            IResult result = await EventEndpoints.ListEventPageAsync(
                Array.Empty<Guid>(), From, To, null, null, skip: 0, take: 10, repository.Object, CancellationToken.None);

            _ = Assert.IsType<BadRequest>(result);
            repository.VerifyNoOtherCalls();
        }

        /// <summary>Verifies that a request naming more devices than the limit is rejected.</summary>
        [Fact]
        public async Task ListEventPageAsync_TooManyDevices_ReturnsBadRequest()
        {
            var repository = new Mock<IEventRepository>();
            Guid[] devices = Enumerable.Range(0, 65).Select(_ => Guid.NewGuid()).ToArray();

            IResult result = await EventEndpoints.ListEventPageAsync(
                devices, From, To, null, null, skip: 0, take: 10, repository.Object, CancellationToken.None);

            _ = Assert.IsType<BadRequest>(result);
            repository.VerifyNoOtherCalls();
        }

        /// <summary>Verifies that from and to are both required.</summary>
        [Fact]
        public async Task ListEventPageAsync_MissingRange_ReturnsBadRequest()
        {
            var repository = new Mock<IEventRepository>();

            IResult result = await EventEndpoints.ListEventPageAsync(
                new[] { Guid.NewGuid() }, From, to: null, null, null, skip: 0, take: 10, repository.Object, CancellationToken.None);

            _ = Assert.IsType<BadRequest>(result);
        }

        /// <summary>Verifies that from later than to is rejected.</summary>
        [Fact]
        public async Task ListEventPageAsync_FromAfterTo_ReturnsBadRequest()
        {
            var repository = new Mock<IEventRepository>();

            IResult result = await EventEndpoints.ListEventPageAsync(
                new[] { Guid.NewGuid() }, To, From, null, null, skip: 0, take: 10, repository.Object, CancellationToken.None);

            _ = Assert.IsType<BadRequest>(result);
        }

        /// <summary>Verifies that take must be between 1 and 500.</summary>
        [Theory]
        [InlineData(0)]
        [InlineData(501)]
        public async Task ListEventPageAsync_TakeOutOfRange_ReturnsBadRequest(int take)
        {
            var repository = new Mock<IEventRepository>();

            IResult result = await EventEndpoints.ListEventPageAsync(
                new[] { Guid.NewGuid() }, From, To, null, null, skip: 0, take: take, repository.Object, CancellationToken.None);

            _ = Assert.IsType<BadRequest>(result);
        }

        /// <summary>Verifies that a negative skip is rejected.</summary>
        [Fact]
        public async Task ListEventPageAsync_NegativeSkip_ReturnsBadRequest()
        {
            var repository = new Mock<IEventRepository>();

            IResult result = await EventEndpoints.ListEventPageAsync(
                new[] { Guid.NewGuid() }, From, To, null, null, skip: -1, take: 10, repository.Object, CancellationToken.None);

            _ = Assert.IsType<BadRequest>(result);
        }

        /// <summary>Verifies that a missing eventIds list is rejected before any query runs.</summary>
        [Fact]
        public async Task GetEventDetailsAsync_MissingIds_ReturnsBadRequest()
        {
            var details = new Mock<IEventDetailRepository>();

            IResult result = await EventEndpoints.GetEventDetailsAsync(null, details.Object, CancellationToken.None);

            _ = Assert.IsType<BadRequest>(result);
            details.VerifyNoOtherCalls();
        }

        /// <summary>Verifies that more than 200 event ids is rejected.</summary>
        [Fact]
        public async Task GetEventDetailsAsync_TooManyIds_ReturnsBadRequest()
        {
            var details = new Mock<IEventDetailRepository>();
            Guid[] ids = Enumerable.Range(0, 201).Select(_ => Guid.NewGuid()).ToArray();

            IResult result = await EventEndpoints.GetEventDetailsAsync(ids, details.Object, CancellationToken.None);

            _ = Assert.IsType<BadRequest>(result);
            details.VerifyNoOtherCalls();
        }

        /// <summary>Verifies that a valid request returns the mapped detail DTOs, one per event that has rows.</summary>
        [Fact]
        public async Task GetEventDetailsAsync_ValidIds_ReturnsMappedDetails()
        {
            var eventId = Guid.NewGuid();
            var detail = new EventDetail(
                eventId,
                new EventDetection { Id = Guid.NewGuid(), EventId = eventId, PersonDetected = true, DetectionType = "Motion" },
                new List<EventDetectionTypeOccurrence>(),
                new List<EventDetectionZone>(),
                new List<EventDetectedPerson>(),
                new List<EventSecurityAlert>());
            var repository = new Mock<IEventDetailRepository>();
            _ = repository
                .Setup(r => r.GetDetailsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<EventDetail> { detail });

            IResult result = await EventEndpoints.GetEventDetailsAsync(new[] { eventId }, repository.Object, CancellationToken.None);

            Ok<IReadOnlyList<EventDetailDto>> ok = Assert.IsType<Ok<IReadOnlyList<EventDetailDto>>>(result);
            EventDetailDto dto = Assert.Single(ok.Value!);
            Assert.Equal(eventId, dto.EventId);
            Assert.Equal("Motion", dto.Detection!.DetectionType);
        }
    }
}
