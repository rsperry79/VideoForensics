using Microsoft.Extensions.Logging;
using Moq;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Providers.Common.Contracts;
using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>
    /// Tests for EvidenceValidationOrchestrator (Phase 3).
    /// Verifies local integrity verification and provider reconciliation.
    /// </summary>
    public class EvidenceValidationOrchestratorTests
    {
        private readonly Mock<ILogger<EvidenceValidationOrchestrator>> _mockLogger = new();
        private readonly Mock<IEventAndConfigService> _mockEventAndConfigService = new();
        private readonly Mock<IEventRepository> _mockEventRepository = new();
        private readonly Mock<IDeviceRepository> _mockDeviceRepository = new();
        private readonly Mock<IIntegrityVerificationService> _mockIntegrityService = new();
        private readonly Mock<IMediaItemRepository> _mockMediaItemRepository = new();
        private readonly Mock<IProviderReconciliationService> _mockReconciliationService = new();

        [Fact]
        public async Task VerifyLocalIntegrityAsync_WithSingleDevice_VerifiesAllMediaItems()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mediaItem1 = new MediaItem
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                FileName = "test1.mp4",
                FilePath = Path.Combine(Path.GetTempPath(), "test1.mp4"),
                MediaFormat = "video/mp4",
                Sha256Hash = "abc123",
                IsPurged = false
            };
            var mediaItem2 = new MediaItem
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                FileName = "test2.mp4",
                FilePath = Path.Combine(Path.GetTempPath(), "test2.mp4"),
                MediaFormat = "video/mp4",
                Sha256Hash = "def456",
                IsPurged = false
            };

            // Create temporary files
            File.WriteAllText(mediaItem1.FilePath, "content1");
            File.WriteAllText(mediaItem2.FilePath, "content2");

            try
            {
                _mockMediaItemRepository.Setup(r => r.GetByDeviceIdAsync(deviceId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<MediaItem> { mediaItem1, mediaItem2 });

                _mockIntegrityService.Setup(s => s.VerifyAsync(mediaItem1.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);
                _mockIntegrityService.Setup(s => s.VerifyAsync(mediaItem2.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

                var orchestrator = new EvidenceValidationOrchestrator(
                    _mockLogger.Object,
                    _mockEventAndConfigService.Object,
                    _mockEventRepository.Object,
                    _mockDeviceRepository.Object,
                    _mockIntegrityService.Object,
                    _mockMediaItemRepository.Object,
                    _mockReconciliationService.Object);

                // Act
                var results = await orchestrator.VerifyLocalIntegrityAsync(deviceId, CancellationToken.None);

                // Assert
                Assert.NotNull(results);
                Assert.Equal(2, results.Count);
                Assert.All(results, r => Assert.Equal("verified", r.Status));
            }
            finally
            {
                // Cleanup
                if (File.Exists(mediaItem1.FilePath))
                    File.Delete(mediaItem1.FilePath);
                if (File.Exists(mediaItem2.FilePath))
                    File.Delete(mediaItem2.FilePath);
            }
        }

        [Fact]
        public async Task VerifyLocalIntegrityAsync_WithFailedItem_ReturnsFailureStatus()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mediaItem = new MediaItem
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                FileName = "test.mp4",
                FilePath = Path.Combine(Path.GetTempPath(), "test_failed.mp4"),
                MediaFormat = "video/mp4",
                Sha256Hash = "abc123",
                IsPurged = false
            };

            File.WriteAllText(mediaItem.FilePath, "content");

            try
            {
                _mockMediaItemRepository.Setup(r => r.GetByDeviceIdAsync(deviceId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<MediaItem> { mediaItem });

                _mockIntegrityService.Setup(s => s.VerifyAsync(mediaItem.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(false); // Verification fails

                var orchestrator = new EvidenceValidationOrchestrator(
                    _mockLogger.Object,
                    _mockEventAndConfigService.Object,
                    _mockEventRepository.Object,
                    _mockDeviceRepository.Object,
                    _mockIntegrityService.Object,
                    _mockMediaItemRepository.Object,
                    _mockReconciliationService.Object);

                // Act
                var results = await orchestrator.VerifyLocalIntegrityAsync(deviceId, CancellationToken.None);

                // Assert
                Assert.Single(results);
                Assert.Equal("failed", results[0].Status);
                Assert.Equal("SHA-256 mismatch against stored hash", results[0].FailureReason);
            }
            finally
            {
                if (File.Exists(mediaItem.FilePath))
                    File.Delete(mediaItem.FilePath);
            }
        }

        [Fact]
        public async Task VerifyLocalIntegrityAsync_WithMissingFile_ReturnsMissingStatus()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mediaItem = new MediaItem
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                FileName = "missing.mp4",
                FilePath = "/nonexistent/path/missing.mp4",
                MediaFormat = "video/mp4",
                Sha256Hash = "abc123",
                IsPurged = false
            };

            _mockMediaItemRepository.Setup(r => r.GetByDeviceIdAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<MediaItem> { mediaItem });

            var orchestrator = new EvidenceValidationOrchestrator(
                _mockLogger.Object,
                _mockEventAndConfigService.Object,
                _mockEventRepository.Object,
                _mockDeviceRepository.Object,
                _mockIntegrityService.Object,
                _mockMediaItemRepository.Object,
                _mockReconciliationService.Object);

            // Act
            var results = await orchestrator.VerifyLocalIntegrityAsync(deviceId, CancellationToken.None);

            // Assert
            Assert.Single(results);
            Assert.Equal("missing", results[0].Status);
            Assert.Contains("not found", results[0].FailureReason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ReconcileWithProviderAsync_WithMissingEvent_ReturnsDiscrepancy()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var providerDeviceId = "ring-device-123";
            var fromUtc = DateTime.UtcNow.AddDays(-1);
            var toUtc = DateTime.UtcNow;

            var storedEvent = new Event
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                ProviderEventId = "evt-123",
                EventType = "motion",
                OccurredAtUtc = DateTime.UtcNow.AddHours(-2),
                SnapshotUrl = "https://example.com/snapshot.jpg"
            };

            var liveEvents = new List<DeviceEvent>(); // Empty - event missing from provider

            _mockEventRepository.Setup(r => r.ListByDeviceAndDateRangeAsync(deviceId, fromUtc, toUtc, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Event> { storedEvent });

            _mockEventAndConfigService.Setup(s => s.GetEventsAsync(providerDeviceId, fromUtc, toUtc, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(liveEvents);

            _mockReconciliationService.Setup(s => s.RecordReconciliationRunAsync(deviceId, It.IsAny<IReadOnlyList<ReconciliationDiscrepancy>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var orchestrator = new EvidenceValidationOrchestrator(
                _mockLogger.Object,
                _mockEventAndConfigService.Object,
                _mockEventRepository.Object,
                _mockDeviceRepository.Object,
                _mockIntegrityService.Object,
                _mockMediaItemRepository.Object,
                _mockReconciliationService.Object);

            // Act
            var discrepancies = await orchestrator.ReconcileWithProviderAsync(deviceId, providerDeviceId, fromUtc, toUtc, CancellationToken.None);

            // Assert
            Assert.Single(discrepancies);
            Assert.Equal(DiscrepancyType.MissingFromProvider, discrepancies[0].Type);
            Assert.Equal("evt-123", discrepancies[0].ProviderEventId);
        }

        [Fact]
        public async Task ReconcileWithProviderAsync_WithMetadataChange_ReturnsMetadataChangedDiscrepancy()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var providerDeviceId = "ring-device-123";
            var fromUtc = DateTime.UtcNow.AddDays(-1);
            var toUtc = DateTime.UtcNow;
            var fixedTime = new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc);

            var storedEvent = new Event
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                ProviderEventId = "evt-123",
                EventType = "motion",
                OccurredAtUtc = fixedTime,
                SnapshotUrl = "https://example.com/snapshot.jpg"
            };

            var liveEvent = new DeviceEvent(
                Id: "evt-123",
                DeviceId: providerDeviceId,
                EventType: "snapshot", // Changed type
                Timestamp: fixedTime,
                SnapshotUrl: "https://example.com/snapshot.jpg"
            );

            _mockEventRepository.Setup(r => r.ListByDeviceAndDateRangeAsync(deviceId, fromUtc, toUtc, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Event> { storedEvent });

            _mockEventAndConfigService.Setup(s => s.GetEventsAsync(providerDeviceId, fromUtc, toUtc, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<DeviceEvent> { liveEvent });

            _mockReconciliationService.Setup(s => s.RecordReconciliationRunAsync(deviceId, It.IsAny<IReadOnlyList<ReconciliationDiscrepancy>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var orchestrator = new EvidenceValidationOrchestrator(
                _mockLogger.Object,
                _mockEventAndConfigService.Object,
                _mockEventRepository.Object,
                _mockDeviceRepository.Object,
                _mockIntegrityService.Object,
                _mockMediaItemRepository.Object,
                _mockReconciliationService.Object);

            // Act
            var discrepancies = await orchestrator.ReconcileWithProviderAsync(deviceId, providerDeviceId, fromUtc, toUtc, CancellationToken.None);

            // Assert
            var eventTypeDiscrepancy = discrepancies.FirstOrDefault(d => d.FieldName == "EventType");
            Assert.NotNull(eventTypeDiscrepancy);
            Assert.Equal(DiscrepancyType.MetadataChanged, eventTypeDiscrepancy.Type);
            Assert.Equal("motion", eventTypeDiscrepancy.StoredValue);
            Assert.Equal("snapshot", eventTypeDiscrepancy.ProviderValue);
        }

        [Fact]
        public async Task ReconcileWithProviderAsync_WithNewEventOnProvider_ReturnsNewEventDiscrepancy()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var providerDeviceId = "ring-device-123";
            var fromUtc = DateTime.UtcNow.AddDays(-1);
            var toUtc = DateTime.UtcNow;

            var storedEvents = new List<Event>(); // No stored events

            var liveEvent = new DeviceEvent(
                Id: "evt-999",
                DeviceId: providerDeviceId,
                EventType: "motion",
                Timestamp: DateTime.UtcNow.AddHours(-1),
                SnapshotUrl: "https://example.com/snapshot.jpg"
            );

            _mockEventRepository.Setup(r => r.ListByDeviceAndDateRangeAsync(deviceId, fromUtc, toUtc, It.IsAny<CancellationToken>()))
                .ReturnsAsync(storedEvents);

            _mockEventAndConfigService.Setup(s => s.GetEventsAsync(providerDeviceId, fromUtc, toUtc, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<DeviceEvent> { liveEvent });

            _mockReconciliationService.Setup(s => s.RecordReconciliationRunAsync(deviceId, It.IsAny<IReadOnlyList<ReconciliationDiscrepancy>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var orchestrator = new EvidenceValidationOrchestrator(
                _mockLogger.Object,
                _mockEventAndConfigService.Object,
                _mockEventRepository.Object,
                _mockDeviceRepository.Object,
                _mockIntegrityService.Object,
                _mockMediaItemRepository.Object,
                _mockReconciliationService.Object);

            // Act
            var discrepancies = await orchestrator.ReconcileWithProviderAsync(deviceId, providerDeviceId, fromUtc, toUtc, CancellationToken.None);

            // Assert
            Assert.Single(discrepancies);
            Assert.Equal(DiscrepancyType.NewEventFoundOnProvider, discrepancies[0].Type);
            Assert.Equal("evt-999", discrepancies[0].ProviderEventId);
        }

        [Fact]
        public async Task ReconcileWithProviderAsync_WithPerfectMatch_ReturnsNoDiscrepancies()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var providerDeviceId = "ring-device-123";
            var fromUtc = DateTime.UtcNow.AddDays(-1);
            var toUtc = DateTime.UtcNow;
            var eventTime = DateTime.UtcNow.AddHours(-2);

            var storedEvent = new Event
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                ProviderEventId = "evt-123",
                EventType = "motion",
                OccurredAtUtc = eventTime,
                SnapshotUrl = "https://example.com/snapshot.jpg"
            };

            var liveEvent = new DeviceEvent(
                Id: "evt-123",
                DeviceId: providerDeviceId,
                EventType: "motion",
                Timestamp: eventTime,
                SnapshotUrl: "https://example.com/snapshot.jpg"
            );

            _mockEventRepository.Setup(r => r.ListByDeviceAndDateRangeAsync(deviceId, fromUtc, toUtc, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Event> { storedEvent });

            _mockEventAndConfigService.Setup(s => s.GetEventsAsync(providerDeviceId, fromUtc, toUtc, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<DeviceEvent> { liveEvent });

            _mockReconciliationService.Setup(s => s.RecordReconciliationRunAsync(deviceId, It.IsAny<IReadOnlyList<ReconciliationDiscrepancy>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var orchestrator = new EvidenceValidationOrchestrator(
                _mockLogger.Object,
                _mockEventAndConfigService.Object,
                _mockEventRepository.Object,
                _mockDeviceRepository.Object,
                _mockIntegrityService.Object,
                _mockMediaItemRepository.Object,
                _mockReconciliationService.Object);

            // Act
            var discrepancies = await orchestrator.ReconcileWithProviderAsync(deviceId, providerDeviceId, fromUtc, toUtc, CancellationToken.None);

            // Assert
            Assert.Empty(discrepancies);
        }
    }
}
