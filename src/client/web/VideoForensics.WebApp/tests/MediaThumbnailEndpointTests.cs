using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Services;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Api;

using Xunit;

namespace VideoForensics.WebApp.Tests
{
    public class MediaThumbnailEndpointTests
    {
        private readonly Mock<IMediaAccessTicketService> _tickets = new();
        private readonly Mock<IOperatorRepository> _operators = new();
        private readonly Mock<IMediaItemRepository> _items = new();
        private readonly Mock<IMediaStorageProvider> _storage = new();
        private readonly Mock<IMediaThumbnailService> _thumbnails = new();
        private readonly Guid _mediaId = Guid.NewGuid();
        private readonly Guid _operatorId = Guid.NewGuid();

        private MediaItem Item(string format = "video/mp4") => new()
        {
            Id = _mediaId,
            DeviceId = Guid.NewGuid(),
            FileName = "clip.mp4",
            FilePath = "/media/clip.mp4",
            MediaFormat = format,
            Sha256Hash = "h",
            RecordedAtUtc = DateTime.UtcNow,
            DownloadedAtUtc = DateTime.UtcNow,
        };

        private Task<IResult> Call(string? ticket = "ticket") =>
            MediaApiEndpoints.GetThumbnailAsync(
                _mediaId, ticket, _tickets.Object, _items.Object, _operators.Object,
                _storage.Object, _thumbnails.Object, new DefaultHttpContext(), CancellationToken.None);

        private void ValidTicketForApprovedOperator()
        {
            _tickets.Setup(t => t.Validate("ticket", _mediaId)).Returns(_operatorId);
            _operators.Setup(o => o.GetAsync(_operatorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Operator
                {
                    Id = _operatorId,
                    DisplayName = "Test",
                    Username = "test",
                    FirstName = "Test",
                    LastName = "Operator",
                    Email = "test@test.com",
                    Active = true,
                    IsApproved = true,
                });
        }

        [Fact]
        public async Task GetThumbnailAsync_NoAuthNoTicket_ReturnsUnauthorized()
        {
            _tickets.Setup(t => t.Validate(null, _mediaId)).Returns((Guid?)null);

            IResult result = await Call(ticket: null);

            Assert.IsType<UnauthorizedHttpResult>(result);
        }

        [Fact]
        public async Task GetThumbnailAsync_UnknownMedia_ReturnsNotFound()
        {
            ValidTicketForApprovedOperator();
            _items.Setup(m => m.GetAsync(_mediaId, It.IsAny<CancellationToken>())).ReturnsAsync((MediaItem?)null);

            IResult result = await Call();

            Assert.IsType<NotFound>(result);
        }

        /// <summary>A video whose frame cannot be produced has no thumbnail to show, so the endpoint says so rather than serving the clip.</summary>
        [Fact]
        public async Task GetThumbnailAsync_NoThumbnailCanBeProduced_ReturnsNotFound()
        {
            ValidTicketForApprovedOperator();
            _items.Setup(m => m.GetAsync(_mediaId, It.IsAny<CancellationToken>())).ReturnsAsync(Item());
            _thumbnails.Setup(t => t.ResolveThumbnailPathAsync(It.IsAny<MediaItem>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);

            IResult result = await Call();

            Assert.IsType<NotFound>(result);
        }

        /// <summary>
        /// Thumbnails are not audited: a gallery load requests one per item, and the audit trail is for full-clip views.
        /// </summary>
        [Fact]
        public async Task GetThumbnailAsync_ValidTicket_ServesStoredFrameWithoutAudit()
        {
            ValidTicketForApprovedOperator();
            _items.Setup(m => m.GetAsync(_mediaId, It.IsAny<CancellationToken>())).ReturnsAsync(Item());
            _thumbnails.Setup(t => t.ResolveThumbnailPathAsync(It.IsAny<MediaItem>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("/media/clip.thumb.png");
            _storage.Setup(s => s.ExistsAsync("/media/clip.thumb.png", It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _storage.Setup(s => s.OpenReadStreamAsync("/media/clip.thumb.png", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MemoryStream(new byte[] { 0x89, 0x50 }));

            IResult result = await Call();

            // The handler takes no audit dependency, so it cannot write an access entry.
            Assert.IsType<FileStreamHttpResult>(result);
        }

        [Fact]
        public async Task GetThumbnailAsync_ServedStoredFrameIsPng()
        {
            ValidTicketForApprovedOperator();
            _items.Setup(m => m.GetAsync(_mediaId, It.IsAny<CancellationToken>())).ReturnsAsync(Item("video/mp4"));
            _thumbnails.Setup(t => t.ResolveThumbnailPathAsync(It.IsAny<MediaItem>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("/media/clip.thumb.png");
            _storage.Setup(s => s.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _storage.Setup(s => s.OpenReadStreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MemoryStream(new byte[] { 0x89 }));

            var served = Assert.IsType<FileStreamHttpResult>(await Call());

            Assert.Equal("image/png", served.ContentType);
        }
    }
}
