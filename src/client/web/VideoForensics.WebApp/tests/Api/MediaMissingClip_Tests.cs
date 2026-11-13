using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;

using Moq;

using System.Security.Claims;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting;
using VideoForensics.Client.WebApp.Api;
using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Api
{
    /// <summary>
    /// Missing-file behaviour of <see cref="MediaApiEndpoints.GetContentAsync"/>. A clip whose file has gone from storage
    /// raises one media-missing alert and queues one integrity check for its device. A repeat request finds the notice
    /// marker and raises nothing new. The response is 404 in every case.
    /// </summary>
    public sealed class MediaMissingClip_Tests
    {
        private const string FilePath = "/missing/clip.mp4";

        private readonly Guid _mediaItemId = Guid.NewGuid();
        private readonly Guid _deviceId = Guid.NewGuid();

        private sealed class Harness
        {
            public Mock<IMediaItemRepository> MediaItems { get; } = new();
            public Mock<IMediaStorageProvider> Storage { get; } = new();
            public Mock<IAccessAuditLogRepository> AuditLog { get; } = new();
            public Mock<INetworkTierResolver> TierResolver { get; } = new();
            public Mock<ILogger<Program>> Logger { get; } = new();
            public Mock<INotificationDispatcher> Dispatcher { get; } = new();
            public Mock<INoticeRepository> Notices { get; } = new();
            public Mock<IDeviceWriteRepository> Devices { get; } = new();
            public Mock<IEvidenceValidationJobs> ValidationJobs { get; } = new();
        }

        private static DefaultHttpContext CreateAuthenticatedContext()
        {
            var claims = new List<Claim>
            {
                new(VideoForensicsClaimTypes.OperatorId, Guid.NewGuid().ToString()),
                new(ClaimTypes.Role, OperatorRole.Admin.ToString())
            };
            return new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
            };
        }

        private Harness CreateHarness(bool fileExists, bool alreadyAlerted = false)
        {
            var h = new Harness();

            _ = h.TierResolver.Setup(t => t.ResolveClientIp(It.IsAny<HttpContext>())).Returns("127.0.0.1");

            _ = h.MediaItems.Setup(m => m.GetAsync(_mediaItemId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MediaItem
                {
                    Id = _mediaItemId,
                    DeviceId = _deviceId,
                    FileName = "clip.mp4",
                    FilePath = FilePath,
                    MediaFormat = "video/mp4",
                    Sha256Hash = "abc123",
                    RecordedAtUtc = DateTime.UtcNow,
                    DownloadedAtUtc = DateTime.UtcNow
                });

            _ = h.Storage.Setup(s => s.ExistsAsync(FilePath, It.IsAny<CancellationToken>()))
                .ReturnsAsync(fileExists);
            _ = h.Storage.Setup(s => s.OpenReadStreamAsync(FilePath, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new MemoryStream(new byte[] { 1, 2, 3 }));

            _ = h.Devices.Setup(d => d.GetAsync(_deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new VideoForensics.Data.Common.Entities.Device
                {
                    Id = _deviceId,
                    ProviderDeviceId = "provider-1",
                    Name = "Front Door",
                    Type = "Doorbell"
                });

            _ = h.Notices.Setup(n => n.ExistsWithDetailsAsync(
                    "media-missing:" + _mediaItemId.ToString("D"), It.IsAny<CancellationToken>()))
                .ReturnsAsync(alreadyAlerted);

            return h;
        }

        private async Task<IResult> InvokeAsync(Harness h, HttpContext context)
        {
            return await MediaApiEndpoints.GetContentAsync(
                _mediaItemId, null, Mock.Of<IMediaAccessTicketService>(), h.MediaItems.Object, Mock.Of<IOperatorRepository>(),
                h.Storage.Object, h.AuditLog.Object, h.TierResolver.Object, h.Logger.Object,
                h.Dispatcher.Object, h.Notices.Object, h.Devices.Object, h.ValidationJobs.Object,
                context, CancellationToken.None);
        }

        [Fact]
        public async Task MediaContent_MissingFile_RaisesAlertAndQueuesValidation()
        {
            Harness h = CreateHarness(fileExists: false);

            IResult result = await InvokeAsync(h, CreateAuthenticatedContext());

            _ = Assert.IsType<NotFound>(result);
            h.Dispatcher.Verify(d => d.DispatchAsync(It.Is<NotificationEvent>(e =>
                e.EventType == "MediaMissing" &&
                e.Severity == NoticeSeverity.Alert &&
                e.Audience == NotificationAudience.All &&
                e.ActionPath == "/media" &&
                e.Details != null &&
                e.Details.Contains("clip.mp4") &&
                e.Details.Contains("Front Door") &&
                e.Details.Contains("media-missing:" + _mediaItemId.ToString("D"))),
                It.IsAny<CancellationToken>()), Times.Once);
            h.ValidationJobs.Verify(j => j.StartIntegrityForMediaItemAsync(_mediaItemId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task MediaContent_MissingFile_AlreadyAlerted_QueuesNothingNew()
        {
            Harness h = CreateHarness(fileExists: false, alreadyAlerted: true);

            IResult result = await InvokeAsync(h, CreateAuthenticatedContext());

            _ = Assert.IsType<NotFound>(result);
            h.Dispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
            h.ValidationJobs.Verify(j => j.StartIntegrityForMediaItemAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task MediaContent_MissingFile_QueuesValidationForThatClipOnly()
        {
            Harness h = CreateHarness(fileExists: false);

            IResult result = await InvokeAsync(h, CreateAuthenticatedContext());

            _ = Assert.IsType<NotFound>(result);
            h.ValidationJobs.Verify(j => j.StartIntegrityForMediaItemAsync(_mediaItemId, It.IsAny<CancellationToken>()), Times.Once);
            h.ValidationJobs.Verify(j => j.StartIntegrityForDevicesAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
            h.ValidationJobs.Verify(j => j.StartIntegrityAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task MediaContent_MissingFile_QueueFails_NoAlertAndNoMarker_RetriesNextTime()
        {
            Harness h = CreateHarness(fileExists: false);
            bool queueWorks = false;
            _ = h.ValidationJobs.Setup(j => j.StartIntegrityForMediaItemAsync(_mediaItemId, It.IsAny<CancellationToken>()))
                .Returns((Guid _, CancellationToken _) => queueWorks
                    ? Task.FromResult<EvidenceValidationJob>(null!)
                    : throw new InvalidOperationException("Another integrity check is running."));

            // First request: the queue throws, so no alert is dispatched and no marker is written. The response is still 404.
            IResult first = await InvokeAsync(h, CreateAuthenticatedContext());
            _ = Assert.IsType<NotFound>(first);
            h.Dispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()), Times.Never);

            // Next request: the queue works, so the alert is dispatched once and the clip is queued again.
            queueWorks = true;
            IResult second = await InvokeAsync(h, CreateAuthenticatedContext());
            _ = Assert.IsType<NotFound>(second);
            h.Dispatcher.Verify(d => d.DispatchAsync(It.Is<NotificationEvent>(e =>
                e.EventType == "MediaMissing" &&
                e.Details != null &&
                e.Details.Contains("media-missing:" + _mediaItemId.ToString("D"))),
                It.IsAny<CancellationToken>()), Times.Once);
            h.ValidationJobs.Verify(j => j.StartIntegrityForMediaItemAsync(_mediaItemId, It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task MediaContent_FileExists_NoAlert()
        {
            Harness h = CreateHarness(fileExists: true);

            IResult result = await InvokeAsync(h, CreateAuthenticatedContext());

            _ = Assert.IsType<FileStreamHttpResult>(result);
            h.Dispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
            h.Notices.Verify(n => n.ExistsWithDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            h.ValidationJobs.Verify(j => j.StartIntegrityForMediaItemAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
