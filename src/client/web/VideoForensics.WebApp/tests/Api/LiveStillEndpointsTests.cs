using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

using Moq;

using System.Security.Claims;
using System.Text;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Core.Services;
using VideoForensics.Client.WebApp.Api;
using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Api.Contracts.Repositories;
using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Api
{
    public class LiveStillEndpointsTests
    {
        private static readonly Guid DeviceId = Guid.NewGuid();
        private static readonly Guid LocationId = Guid.NewGuid();
        private static readonly Guid AccountId = Guid.NewGuid();
        private static readonly Guid OperatorId = Guid.NewGuid();
        private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4];

        private readonly Mock<ILiveStillStore> _stills = new();
        private readonly Mock<IDeviceRepository> _devices = new();
        private readonly Mock<ILocationRepository> _locations = new();
        private readonly Mock<IProviderAccountRepository> _accounts = new();
        private readonly Mock<IAccountDeviceRepository> _accountDevices = new();
        private readonly Mock<IForensicsConfigurationService> _config = new();
        private readonly Mock<IStorageLocationProvider> _storageLocation = new();
        private readonly Mock<IMediaItemWriteRepository> _media = new();
        private readonly Mock<IMediaStorageProvider> _storage = new();

        public LiveStillEndpointsTests()
        {
            var device = new DeviceDto(DeviceId, LocationId, "123", "Front Door", "doorbot", true, null, null, null,
                null, SyncStatusDto.Synced);
            _ = _devices.Setup(d => d.GetAsync(DeviceId, It.IsAny<CancellationToken>())).ReturnsAsync(device);
            _ = _locations.Setup(l => l.GetAsync(LocationId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LocationRecordDto(LocationId, "loc-1", "Home", null, null, default));
            _ = _accounts.Setup(a => a.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ProviderAccountDto> { Account(AccountId, "Ring") });
            _ = _accountDevices.Setup(a => a.ListByProviderAccountAsync(AccountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Device> { new() { Id = DeviceId, LocationId = LocationId, ProviderDeviceId = "123", Name = "Front Door", Type = "doorbot" } });

            var configuration = new Mock<IForensicsConfiguration>();
            _ = _config.Setup(c => c.LoadConfigurationAsync(string.Empty, It.IsAny<CancellationToken>())).ReturnsAsync(configuration.Object);
            _ = _storageLocation.Setup(s => s.GetEffectiveRoot(StorageCategory.Media, It.IsAny<string?>())).Returns(@"F:\Media");
        }

        private static ProviderAccountDto Account(Guid id, string provider) => new(
            id, Guid.NewGuid(), provider, DateTime.UtcNow, null, true, null, null, null);

        private HttpContext Context(string? contentType = "image/jpeg", bool signedIn = true, byte[]? body = null)
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

        private Task<IResult> Upload(HttpContext context, Guid? deviceId = null) => LiveStillEndpoints.UploadAsync(
            deviceId ?? DeviceId,
            context.Request,
            _stills.Object,
            _devices.Object,
            _locations.Object,
            _accounts.Object,
            _accountDevices.Object,
            _config.Object,
            _storageLocation.Object,
            context,
            CancellationToken.None);

        [Fact]
        public async Task UploadAsync_NotSignedIn_ReturnsUnauthorized()
        {
            IResult result = await Upload(Context(signedIn: false));

            Assert.Equal(StatusCodes.Status401Unauthorized, ((IStatusCodeHttpResult)result).StatusCode);
            _stills.Verify(s => s.StoreAsync(It.IsAny<LiveStillRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UploadAsync_NotJpegContentType_ReturnsUnsupportedMediaType()
        {
            IResult result = await Upload(Context(contentType: "image/png"));

            Assert.Equal(StatusCodes.Status415UnsupportedMediaType, ((IStatusCodeHttpResult)result).StatusCode);
            _stills.Verify(s => s.StoreAsync(It.IsAny<LiveStillRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UploadAsync_OversizedBody_ReturnsPayloadTooLarge()
        {
            byte[] big = new byte[(20 * 1024 * 1024) + 1];
            big[0] = 0xFF; big[1] = 0xD8; big[2] = 0xFF;

            IResult result = await Upload(Context(body: big));

            Assert.Equal(StatusCodes.Status413PayloadTooLarge, ((IStatusCodeHttpResult)result).StatusCode);
            _stills.Verify(s => s.StoreAsync(It.IsAny<LiveStillRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UploadAsync_UnknownDevice_ReturnsNotFound()
        {
            var unknown = Guid.NewGuid();
            _ = _devices.Setup(d => d.GetAsync(unknown, It.IsAny<CancellationToken>())).ReturnsAsync((DeviceDto?)null);

            IResult result = await Upload(Context(), unknown);

            Assert.Equal(StatusCodes.Status404NotFound, ((IStatusCodeHttpResult)result).StatusCode);
        }

        [Fact]
        public async Task UploadAsync_DeviceWithNoAccount_ReturnsNotFound()
        {
            _ = _accounts.Setup(a => a.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ProviderAccountDto>());

            IResult result = await Upload(Context());

            Assert.Equal(StatusCodes.Status404NotFound, ((IStatusCodeHttpResult)result).StatusCode);
        }

        [Fact]
        public async Task UploadAsync_Valid_StoresWithMediaRootProviderAndNames_Returns201()
        {
            var stillId = Guid.NewGuid();
            _ = _stills
                .Setup(s => s.StoreAsync(It.IsAny<LiveStillRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(LiveStillResult.Ok(stillId));

            IResult result = await Upload(Context());

            Assert.Equal(StatusCodes.Status201Created, ((IStatusCodeHttpResult)result).StatusCode);
            _stills.Verify(s => s.StoreAsync(
                It.Is<LiveStillRequest>(r =>
                    r.DeviceId == DeviceId
                    && r.MediaRoot == @"F:\Media"
                    && r.ProviderName == "Ring"
                    && r.LocationName == "Home"
                    && r.DeviceName == "Front Door"
                    && r.OperatorName == OperatorId.ToString()
                    && r.Jpeg.SequenceEqual(Jpeg)),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData(LiveStillError.InvalidInput, StatusCodes.Status400BadRequest)]
        [InlineData(LiveStillError.Conflict, StatusCodes.Status409Conflict)]
        [InlineData(LiveStillError.StorageFailed, StatusCodes.Status500InternalServerError)]
        public async Task UploadAsync_StoreFailure_MapsToStatus(LiveStillError error, int status)
        {
            _ = _stills
                .Setup(s => s.StoreAsync(It.IsAny<LiveStillRequest>(), It.IsAny<CancellationToken>()))
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
