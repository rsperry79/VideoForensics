using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

using Moq;

using System.Security.Claims;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.WebApp.Api;
using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Api
{
    public class LiveStillEndpointsTests
    {
        private static readonly Guid DeviceId = Guid.NewGuid();
        private static readonly Guid OperatorId = Guid.NewGuid();
        private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4];

        private readonly Mock<ILiveStillCaptureService> _capture = new();
        private readonly Mock<IMediaItemWriteRepository> _media = new();
        private readonly Mock<IMediaStorageProvider> _storage = new();

        private static HttpContext Context(string? contentType = "image/jpeg", bool signedIn = true, byte[]? body = null)
        {
            var context = new DefaultHttpContext();
            if (signedIn)
            {
                context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(VideoForensicsClaimTypes.OperatorId, OperatorId.ToString())
                }, "test"));
            }

            context.Request.ContentType = contentType;
            context.Request.Body = new MemoryStream(body ?? Jpeg);
            return context;
        }

        private Task<IResult> Upload(HttpContext context) => LiveStillEndpoints.UploadAsync(
            DeviceId, context.Request, _capture.Object, context, CancellationToken.None);

        [Fact]
        public async Task UploadAsync_NotSignedIn_ReturnsUnauthorized()
        {
            IResult result = await Upload(Context(signedIn: false));

            Assert.Equal(StatusCodes.Status401Unauthorized, ((IStatusCodeHttpResult)result).StatusCode);
            _capture.Verify(c => c.CaptureAsync(It.IsAny<Guid>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UploadAsync_NotJpegContentType_ReturnsUnsupportedMediaType()
        {
            IResult result = await Upload(Context(contentType: "image/png"));

            Assert.Equal(StatusCodes.Status415UnsupportedMediaType, ((IStatusCodeHttpResult)result).StatusCode);
            _capture.Verify(c => c.CaptureAsync(It.IsAny<Guid>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UploadAsync_OversizedBody_ReturnsPayloadTooLarge()
        {
            byte[] big = new byte[(20 * 1024 * 1024) + 1];
            big[0] = 0xFF; big[1] = 0xD8; big[2] = 0xFF;

            IResult result = await Upload(Context(body: big));

            Assert.Equal(StatusCodes.Status413PayloadTooLarge, ((IStatusCodeHttpResult)result).StatusCode);
            _capture.Verify(c => c.CaptureAsync(It.IsAny<Guid>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UploadAsync_Valid_PassesCameraJpegAndOperatorToCapture_Returns201()
        {
            var stillId = Guid.NewGuid();
            _ = _capture
                .Setup(c => c.CaptureAsync(DeviceId, It.IsAny<byte[]>(), OperatorId.ToString(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(LiveStillResult.Ok(stillId));

            IResult result = await Upload(Context());

            Assert.Equal(StatusCodes.Status201Created, ((IStatusCodeHttpResult)result).StatusCode);
            _capture.Verify(c => c.CaptureAsync(DeviceId, It.Is<byte[]>(b => b.SequenceEqual(Jpeg)), OperatorId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData(LiveStillError.NotFound, StatusCodes.Status404NotFound)]
        [InlineData(LiveStillError.InvalidInput, StatusCodes.Status400BadRequest)]
        [InlineData(LiveStillError.Conflict, StatusCodes.Status409Conflict)]
        [InlineData(LiveStillError.StorageFailed, StatusCodes.Status500InternalServerError)]
        public async Task UploadAsync_CaptureFailure_MapsToStatus(LiveStillError error, int status)
        {
            _ = _capture
                .Setup(c => c.CaptureAsync(It.IsAny<Guid>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(LiveStillResult.Fail(error, "reason"));

            IResult result = await Upload(Context());

            Assert.Equal(status, ((IStatusCodeHttpResult)result).StatusCode);
        }

        [Fact]
        public async Task GetStillAsync_MissingItem_ReturnsNotFound()
        {
            var id = Guid.NewGuid();
            _ = _media.Setup(m => m.GetAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((MediaItem?)null);

            IResult result = await LiveStillEndpoints.GetStillAsync(id, _media.Object, _storage.Object, CancellationToken.None);

            Assert.Equal(StatusCodes.Status404NotFound, ((IStatusCodeHttpResult)result).StatusCode);
        }

        [Fact]
        public async Task GetStillAsync_NotAStill_ReturnsNotFound()
        {
            var id = Guid.NewGuid();
            _ = _media.Setup(m => m.GetAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new MediaItem
            {
                Id = id, DeviceId = DeviceId, FileName = "clip.mp4", FilePath = "clip.mp4", MediaFormat = "mp4",
                MediaKind = MediaKinds.Clip, RecordedAtUtc = DateTime.UtcNow, DownloadedAtUtc = DateTime.UtcNow, Sha256Hash = "x"
            });

            IResult result = await LiveStillEndpoints.GetStillAsync(id, _media.Object, _storage.Object, CancellationToken.None);

            Assert.Equal(StatusCodes.Status404NotFound, ((IStatusCodeHttpResult)result).StatusCode);
        }

        [Fact]
        public async Task GetStillAsync_Still_StreamsTheJpeg()
        {
            var id = Guid.NewGuid();
            _ = _media.Setup(m => m.GetAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new MediaItem
            {
                Id = id, DeviceId = DeviceId, FileName = "s.jpg", FilePath = "stills/s.jpg", MediaFormat = "jpg",
                MediaKind = MediaKinds.Still, RecordedAtUtc = DateTime.UtcNow, DownloadedAtUtc = DateTime.UtcNow, Sha256Hash = "x"
            });
            _ = _storage.Setup(s => s.ExistsAsync("stills/s.jpg", It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _ = _storage.Setup(s => s.OpenReadStreamAsync("stills/s.jpg", It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new MemoryStream(Jpeg));

            IResult result = await LiveStillEndpoints.GetStillAsync(id, _media.Object, _storage.Object, CancellationToken.None);

            var stream = Assert.IsType<FileStreamHttpResult>(result);
            Assert.Equal("image/jpeg", stream.ContentType);
        }
    }
}
