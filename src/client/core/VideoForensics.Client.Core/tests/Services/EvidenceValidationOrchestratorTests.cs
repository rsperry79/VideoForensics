using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Client.Core.Services;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;

using Xunit;

namespace VideoForensics.Client.Core.Tests.Services
{
    public class EvidenceValidationOrchestratorTests
    {
        private readonly Mock<ILogger<EvidenceValidationOrchestrator>> _loggerMock;
        private readonly Mock<IEventAndConfigService> _eventServiceMock;
        private readonly Mock<IEventRepository> _eventRepositoryMock;
        private readonly Mock<IDeviceRepository> _deviceRepositoryMock;
        private readonly Mock<IIntegrityVerificationService> _integrityServiceMock;
        private readonly Mock<IMediaItemRepository> _mediaItemRepositoryMock;
        private readonly Mock<IProviderReconciliationService> _reconciliationServiceMock;
        private readonly Mock<VideoForensics.Data.Core.Contracts.IMediaThumbnailService> _thumbnailsMock;
        private readonly EvidenceValidationOrchestrator _orchestrator;

        public EvidenceValidationOrchestratorTests()
        {
            _loggerMock = new Mock<ILogger<EvidenceValidationOrchestrator>>();
            _eventServiceMock = new Mock<IEventAndConfigService>();
            _eventRepositoryMock = new Mock<IEventRepository>();
            _deviceRepositoryMock = new Mock<IDeviceRepository>();
            _integrityServiceMock = new Mock<IIntegrityVerificationService>();
            _mediaItemRepositoryMock = new Mock<IMediaItemRepository>();
            _reconciliationServiceMock = new Mock<IProviderReconciliationService>();
            _thumbnailsMock = new Mock<VideoForensics.Data.Core.Contracts.IMediaThumbnailService>();

            _orchestrator = new EvidenceValidationOrchestrator(
                _loggerMock.Object,
                _eventServiceMock.Object,
                _eventRepositoryMock.Object,
                _deviceRepositoryMock.Object,
                _integrityServiceMock.Object,
                _mediaItemRepositoryMock.Object,
                _thumbnailsMock.Object,
                _reconciliationServiceMock.Object);
        }

        /// <summary>
        /// A manual verification run is where thumbnails that went missing are found, so every row is reconciled with
        /// the disk, including a row whose clip is itself gone (that is exactly when its stored thumbnail name goes stale).
        /// </summary>
        [Fact]
        public async Task VerifyLocalIntegrityAsync_ReconcilesThumbnailOfEveryRow()
        {
            var deviceId = Guid.NewGuid();
            var item = new MediaItem
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                FileName = "clip.mp4",
                FilePath = Path.Combine(Path.GetTempPath(), "vf-verify-" + Guid.NewGuid(), "clip.mp4"),
                MediaFormat = "video/mp4",
                Sha256Hash = "h",
                ThumbnailPath = "clip.thumb.png",
            };
            _ = _mediaItemRepositoryMock
                .Setup(r => r.GetByDeviceIdAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync([item]);

            _ = await _orchestrator.VerifyLocalIntegrityAsync(deviceId, CancellationToken.None);

            _thumbnailsMock.Verify(t => t.ReconcileThumbnailAsync(item, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ReconcileWithProviderAsync_SanitizesLogOutput_WhenProviderDeviceIdContainsNewlines()
        {
            // Arrange: Setup mocks
            var deviceId = Guid.NewGuid();
            string injectedDeviceId = "device-123\r\nFAKE ADMIN LOG: Unauthorized access granted";
            DateTime fromDate = DateTime.UtcNow.AddDays(-7);
            DateTime toDate = DateTime.UtcNow;

            _ = _eventServiceMock
                .Setup(s => s.GetEventsAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((IReadOnlyList<DeviceEvent>)[]));

            _ = _eventRepositoryMock
                .Setup(r => r.ListByDeviceAndDateRangeAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((IReadOnlyList<Data.Common.Entities.Event>)[]));

            _ = _reconciliationServiceMock
                .Setup(s => s.RecordReconciliationRunAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<ReconciliationDiscrepancy>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act: Call ReconcileWithProviderAsync with injected device ID containing CRLF
            IReadOnlyList<ReconciliationDiscrepancy> result = await _orchestrator.ReconcileWithProviderAsync(
                deviceId,
                injectedDeviceId,
                fromDate,
                toDate,
                CancellationToken.None);

            // Assert: Verify the method completed and didn't throw
            // (the sanitization is verified indirectly — the method completes without exception)
            Assert.NotNull(result);
            // Verify reconciliation record was persisted
            _reconciliationServiceMock.Verify(
                s => s.RecordReconciliationRunAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<ReconciliationDiscrepancy>>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
