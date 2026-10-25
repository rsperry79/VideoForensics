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
    /// Tests for the paged media-items endpoint the media viewer uses: validation at the API boundary and the page it returns.
    /// </summary>
    public class MediaApiEndpointsPageTests
    {
        private static readonly DateTime From = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime To = From.AddHours(1);

        /// <summary>Verifies that a valid request asks the repository for the requested slice and returns its total count.</summary>
        [Fact]
        public async Task ListMediaItemPageAsync_ValidRequest_ReturnsPageWithTotalCount()
        {
            var repository = new Mock<IMediaItemRepository>();
            _ = repository
                .Setup(r => r.GetPageByDevicesAndDateRangeAsync(It.IsAny<IReadOnlyCollection<Guid>>(), From, To, 10, 25, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MediaItemPage(new List<MediaItem>(), 42));

            IResult result = await MediaApiEndpoints.ListMediaItemPageAsync(
                new[] { Guid.NewGuid(), Guid.NewGuid() }, From, To, skip: 10, take: 25, repository.Object, CancellationToken.None);

            Ok<MediaItemPageDto> ok = Assert.IsType<Ok<MediaItemPageDto>>(result);
            Assert.Equal(42, ok.Value!.TotalCount);
        }

        /// <summary>Verifies that omitting skip and take uses the default page, from zero with 100 items.</summary>
        [Fact]
        public async Task ListMediaItemPageAsync_NoPaging_UsesDefaultPage()
        {
            var repository = new Mock<IMediaItemRepository>();
            _ = repository
                .Setup(r => r.GetPageByDevicesAndDateRangeAsync(It.IsAny<IReadOnlyCollection<Guid>>(), From, To, 0, 100, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MediaItemPage(new List<MediaItem>(), 0));

            IResult result = await MediaApiEndpoints.ListMediaItemPageAsync(
                new[] { Guid.NewGuid() }, From, To, skip: null, take: null, repository.Object, CancellationToken.None);

            _ = Assert.IsType<Ok<MediaItemPageDto>>(result);
            repository.Verify(r => r.GetPageByDevicesAndDateRangeAsync(It.IsAny<IReadOnlyCollection<Guid>>(), From, To, 0, 100, It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>Verifies that a request with no device IDs is rejected before any query runs.</summary>
        [Fact]
        public async Task ListMediaItemPageAsync_NoDevices_ReturnsBadRequest()
        {
            var repository = new Mock<IMediaItemRepository>();

            IResult result = await MediaApiEndpoints.ListMediaItemPageAsync(
                Array.Empty<Guid>(), From, To, skip: 0, take: 10, repository.Object, CancellationToken.None);

            _ = Assert.IsType<BadRequest>(result);
            repository.VerifyNoOtherCalls();
        }

        /// <summary>Verifies that from and to are both required.</summary>
        [Fact]
        public async Task ListMediaItemPageAsync_MissingRange_ReturnsBadRequest()
        {
            var repository = new Mock<IMediaItemRepository>();

            IResult result = await MediaApiEndpoints.ListMediaItemPageAsync(
                new[] { Guid.NewGuid() }, From, to: null, skip: 0, take: 10, repository.Object, CancellationToken.None);

            _ = Assert.IsType<BadRequest>(result);
        }

        /// <summary>Verifies that from later than to is rejected.</summary>
        [Fact]
        public async Task ListMediaItemPageAsync_FromAfterTo_ReturnsBadRequest()
        {
            var repository = new Mock<IMediaItemRepository>();

            IResult result = await MediaApiEndpoints.ListMediaItemPageAsync(
                new[] { Guid.NewGuid() }, To, From, skip: 0, take: 10, repository.Object, CancellationToken.None);

            _ = Assert.IsType<BadRequest>(result);
        }

        /// <summary>Verifies that take must be between 1 and 500, so a client cannot ask for an unbounded page.</summary>
        [Theory]
        [InlineData(0)]
        [InlineData(501)]
        public async Task ListMediaItemPageAsync_TakeOutOfRange_ReturnsBadRequest(int take)
        {
            var repository = new Mock<IMediaItemRepository>();

            IResult result = await MediaApiEndpoints.ListMediaItemPageAsync(
                new[] { Guid.NewGuid() }, From, To, skip: 0, take: take, repository.Object, CancellationToken.None);

            _ = Assert.IsType<BadRequest>(result);
        }

        /// <summary>Verifies that a negative skip is rejected.</summary>
        [Fact]
        public async Task ListMediaItemPageAsync_NegativeSkip_ReturnsBadRequest()
        {
            var repository = new Mock<IMediaItemRepository>();

            IResult result = await MediaApiEndpoints.ListMediaItemPageAsync(
                new[] { Guid.NewGuid() }, From, To, skip: -1, take: 10, repository.Object, CancellationToken.None);

            _ = Assert.IsType<BadRequest>(result);
        }

        /// <summary>Verifies that a request naming more devices than the limit is rejected, keeping the SQL IN list bounded.</summary>
        [Fact]
        public async Task ListMediaItemPageAsync_TooManyDevices_ReturnsBadRequest()
        {
            var repository = new Mock<IMediaItemRepository>();
            Guid[] devices = Enumerable.Range(0, 65).Select(_ => Guid.NewGuid()).ToArray();

            IResult result = await MediaApiEndpoints.ListMediaItemPageAsync(
                devices, From, To, skip: 0, take: 10, repository.Object, CancellationToken.None);

            _ = Assert.IsType<BadRequest>(result);
        }
    }
}
