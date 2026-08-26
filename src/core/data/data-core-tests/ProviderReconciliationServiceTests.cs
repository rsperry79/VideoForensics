using Microsoft.Extensions.Logging;
using Moq;
using VideoForensics.Core.Logging.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Data.Core.Services;
using Xunit;

namespace VideoForensics.Data.Core.Tests
{
    public class ProviderReconciliationServiceTests
    {
        private readonly Mock<IProviderReconciliationRepository> _mockReconciliationRepository;
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IActionLogger> _mockActionLogger;
        private readonly Mock<ILogger<ProviderReconciliationService>> _mockLogger;
        private readonly ProviderReconciliationService _service;

        public ProviderReconciliationServiceTests()
        {
            _mockReconciliationRepository = new Mock<IProviderReconciliationRepository>();
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockActionLogger = new Mock<IActionLogger>();
            _mockLogger = new Mock<ILogger<ProviderReconciliationService>>();

            _service = new ProviderReconciliationService(
                _mockReconciliationRepository.Object,
                _mockUnitOfWork.Object,
                _mockActionLogger.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task RecordReconciliationRunAsync_WithDiscrepancies_LogsSingleActionLogEntry()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var discrepancies = new List<ReconciliationDiscrepancy>
            {
                new ReconciliationDiscrepancy { ProviderEventId = "event-1", Type = DiscrepancyType.MissingFromProvider },
                new ReconciliationDiscrepancy { ProviderEventId = "event-2", Type = DiscrepancyType.MetadataChanged, FieldName = "timestamp" }
            };

            var mockContext = new Mock<IUnitOfWorkContext>();
            var mockReconciliationRepoInContext = new Mock<IProviderReconciliationRepository>();
            var mockActionLogRepoInContext = new Mock<IActionLogRepository>();

            mockContext.Setup(x => x.ProviderReconciliation).Returns(mockReconciliationRepoInContext.Object);
            mockContext.Setup(x => x.ActionLog).Returns(mockActionLogRepoInContext.Object);

            var expectedLogEntry = new ActionLogEntry
            {
                Id = Guid.NewGuid(),
                Actor = Environment.UserName,
                ActorType = ActorType.Human,
                Action = "ProviderReconciliationRun",
                EntityType = "Device",
                EntityId = deviceId,
                TimestampUtc = DateTime.UtcNow
            ,
                EntryHash = "test_hash"};

            mockReconciliationRepoInContext
                .Setup(x => x.AppendAsync(It.IsAny<ProviderReconciliationRecord>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProviderReconciliationRecord record, CancellationToken ct) => record);

            mockActionLogRepoInContext
                .Setup(x => x.AppendAsync(
                    It.IsAny<string>(),
                    It.IsAny<ActorType>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedLogEntry);

            _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(
                    It.IsAny<Func<IUnitOfWorkContext, Task<bool>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<bool>> work, CancellationToken ct) =>
                    await work(mockContext.Object));

            // Act
            await _service.RecordReconciliationRunAsync(deviceId, discrepancies, CancellationToken.None);

            // Assert
            // Verify action log was called exactly once
            mockActionLogRepoInContext.Verify(
                x => x.AppendAsync(
                    Environment.UserName,
                    ActorType.Human,
                    "ProviderReconciliationRun",
                    "Device",
                    deviceId,
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            // Verify each discrepancy was appended
            mockReconciliationRepoInContext.Verify(
                x => x.AppendAsync(It.IsAny<ProviderReconciliationRecord>(), It.IsAny<CancellationToken>()),
                Times.Exactly(discrepancies.Count));
        }

        [Fact]
        public async Task RecordReconciliationRunAsync_LogsCorrectDiscrepancyCounts()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var discrepancies = new List<ReconciliationDiscrepancy>
            {
                new ReconciliationDiscrepancy { ProviderEventId = "event-1", Type = DiscrepancyType.MissingFromProvider },
                new ReconciliationDiscrepancy { ProviderEventId = "event-2", Type = DiscrepancyType.MissingFromProvider },
                new ReconciliationDiscrepancy { ProviderEventId = "event-3", Type = DiscrepancyType.MetadataChanged },
                new ReconciliationDiscrepancy { ProviderEventId = "event-4", Type = DiscrepancyType.NewEventFoundOnProvider }
            };

            var mockContext = new Mock<IUnitOfWorkContext>();
            var mockReconciliationRepoInContext = new Mock<IProviderReconciliationRepository>();
            var mockActionLogRepoInContext = new Mock<IActionLogRepository>();

            mockContext.Setup(x => x.ProviderReconciliation).Returns(mockReconciliationRepoInContext.Object);
            mockContext.Setup(x => x.ActionLog).Returns(mockActionLogRepoInContext.Object);

            mockReconciliationRepoInContext
                .Setup(x => x.AppendAsync(It.IsAny<ProviderReconciliationRecord>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProviderReconciliationRecord record, CancellationToken ct) => record);

            string? capturedDetails = null;
            mockActionLogRepoInContext
                .Setup(x => x.AppendAsync(
                    It.IsAny<string>(),
                    It.IsAny<ActorType>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, ActorType, string, string, Guid?, string, CancellationToken>(
                    (actor, type, action, entity, id, details, ct) => { capturedDetails = details; })
                .ReturnsAsync(TestHelpers.CreateActionLogEntry());

            _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(
                    It.IsAny<Func<IUnitOfWorkContext, Task<bool>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<bool>> work, CancellationToken ct) =>
                    await work(mockContext.Object));

            // Act
            await _service.RecordReconciliationRunAsync(deviceId, discrepancies, CancellationToken.None);

            // Assert
            Assert.NotNull(capturedDetails);
            Assert.Contains("TotalDiscrepancies", capturedDetails);
            Assert.Contains("4", capturedDetails);
            Assert.Contains("MissingFromProvider", capturedDetails);
            Assert.Contains("2", capturedDetails);
            Assert.Contains("MetadataChanged", capturedDetails);
            Assert.Contains("1", capturedDetails);
        }

        [Fact]
        public async Task RecordReconciliationRunAsync_WithEmptyDiscrepancies_LogsZeroCounts()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var discrepancies = new List<ReconciliationDiscrepancy>();

            var mockContext = new Mock<IUnitOfWorkContext>();
            var mockReconciliationRepoInContext = new Mock<IProviderReconciliationRepository>();
            var mockActionLogRepoInContext = new Mock<IActionLogRepository>();

            mockContext.Setup(x => x.ProviderReconciliation).Returns(mockReconciliationRepoInContext.Object);
            mockContext.Setup(x => x.ActionLog).Returns(mockActionLogRepoInContext.Object);

            string? capturedDetails = null;
            mockActionLogRepoInContext
                .Setup(x => x.AppendAsync(
                    It.IsAny<string>(),
                    It.IsAny<ActorType>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, ActorType, string, string, Guid?, string, CancellationToken>(
                    (actor, type, action, entity, id, details, ct) => { capturedDetails = details; })
                .ReturnsAsync(TestHelpers.CreateActionLogEntry());

            _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(
                    It.IsAny<Func<IUnitOfWorkContext, Task<bool>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<bool>> work, CancellationToken ct) =>
                    await work(mockContext.Object));

            // Act
            await _service.RecordReconciliationRunAsync(deviceId, discrepancies, CancellationToken.None);

            // Assert
            Assert.NotNull(capturedDetails);
            Assert.Contains("TotalDiscrepancies", capturedDetails);
            Assert.Contains("0", capturedDetails);
        }

        [Fact]
        public async Task GetHistoryAsync_ReturnsReconciliationHistory()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var expectedRecords = new List<ProviderReconciliationRecord>
            {
                new ProviderReconciliationRecord
                {
                    Id = Guid.NewGuid(),
                    DeviceId = deviceId,
                    RanAtUtc = DateTime.UtcNow.AddHours(-1),
                    ProviderEventId = "event-1",
                    DiscrepancyType = DiscrepancyType.MissingFromProvider
                },
                new ProviderReconciliationRecord
                {
                    Id = Guid.NewGuid(),
                    DeviceId = deviceId,
                    RanAtUtc = DateTime.UtcNow,
                    ProviderEventId = "event-2",
                    DiscrepancyType = DiscrepancyType.MetadataChanged,
                    FieldName = "timestamp"
                }
            };

            _mockReconciliationRepository
                .Setup(x => x.GetHistoryForDeviceAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedRecords);

            // Act
            var result = await _service.GetHistoryAsync(deviceId, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal(expectedRecords[0].Id, result[0].Id);
            Assert.Equal(expectedRecords[1].Id, result[1].Id);
            _mockReconciliationRepository.Verify(
                x => x.GetHistoryForDeviceAsync(deviceId, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GetHistoryAsync_WithEmptyHistory_ReturnsEmptyList()
        {
            // Arrange
            var deviceId = Guid.NewGuid();

            _mockReconciliationRepository
                .Setup(x => x.GetHistoryForDeviceAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ProviderReconciliationRecord>());

            // Act
            var result = await _service.GetHistoryAsync(deviceId, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task RecordReconciliationRunAsync_PreservesDiscrepancyDetails()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var discrepancies = new List<ReconciliationDiscrepancy>
            {
                new ReconciliationDiscrepancy
                {
                    ProviderEventId = "event-123",
                    Type = DiscrepancyType.MetadataChanged,
                    FieldName = "EventType",
                    StoredValue = "Motion",
                    ProviderValue = "Sound"
                }
            };

            var mockContext = new Mock<IUnitOfWorkContext>();
            var mockReconciliationRepoInContext = new Mock<IProviderReconciliationRepository>();
            var mockActionLogRepoInContext = new Mock<IActionLogRepository>();

            mockContext.Setup(x => x.ProviderReconciliation).Returns(mockReconciliationRepoInContext.Object);
            mockContext.Setup(x => x.ActionLog).Returns(mockActionLogRepoInContext.Object);

            ProviderReconciliationRecord? capturedRecord = null;
            mockReconciliationRepoInContext
                .Setup(x => x.AppendAsync(It.IsAny<ProviderReconciliationRecord>(), It.IsAny<CancellationToken>()))
                .Callback<ProviderReconciliationRecord, CancellationToken>((record, ct) => { capturedRecord = record; })
                .ReturnsAsync((ProviderReconciliationRecord record, CancellationToken ct) => record);

            mockActionLogRepoInContext
                .Setup(x => x.AppendAsync(
                    It.IsAny<string>(),
                    It.IsAny<ActorType>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestHelpers.CreateActionLogEntry());

            _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(
                    It.IsAny<Func<IUnitOfWorkContext, Task<bool>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<bool>> work, CancellationToken ct) =>
                    await work(mockContext.Object));

            // Act
            await _service.RecordReconciliationRunAsync(deviceId, discrepancies, CancellationToken.None);

            // Assert
            Assert.NotNull(capturedRecord);
            Assert.Equal("event-123", capturedRecord.ProviderEventId);
            Assert.Equal(DiscrepancyType.MetadataChanged, capturedRecord.DiscrepancyType);
            Assert.Equal("EventType", capturedRecord.FieldName);
            Assert.Equal("Motion", capturedRecord.StoredValue);
            Assert.Equal("Sound", capturedRecord.ProviderValue);
        }
    }
}
