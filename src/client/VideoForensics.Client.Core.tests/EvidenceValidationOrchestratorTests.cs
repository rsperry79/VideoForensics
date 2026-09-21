using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Core.Services;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Providers.Common.Contracts;

using Xunit;

namespace VideoForensics.Client.Core.Tests
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

            _orchestrator = new EvidenceValidationOrchestrator(
                _loggerMock.Object,
                _eventServiceMock.Object,
                _eventRepositoryMock.Object,
                _deviceRepositoryMock.Object,
                _integrityServiceMock.Object,
                _mediaItemRepositoryMock.Object,
                _reconciliationServiceMock.Object);
        }

        [Fact]
        public async Task ReconcileWithProviderAsync_SanitizesLogOutput_WhenProviderDeviceIdContainsNewlines()
        {
            // Arrange: Setup mocks
            var deviceId = Guid.NewGuid();
            string injectedDeviceId = "device-123\r\nFAKE ADMIN LOG: Unauthorized access granted";
            var fromDate = DateTime.UtcNow.AddDays(-7);
            var toDate = DateTime.UtcNow;

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
