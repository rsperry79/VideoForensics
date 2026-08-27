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
    public class ExportRecordServiceTests
    {
        private readonly Mock<IExportRecordRepository> _mockExportRecordRepository;
        private readonly Mock<IMediaItemRepository> _mockMediaItemRepository;
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IActionLogger> _mockActionLogger;
        private readonly Mock<ILogger<ExportRecordService>> _mockLogger;
        private readonly ExportRecordService _service;

        public ExportRecordServiceTests()
        {
            _mockExportRecordRepository = new Mock<IExportRecordRepository>();
            _mockMediaItemRepository = new Mock<IMediaItemRepository>();
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockActionLogger = new Mock<IActionLogger>();
            _mockLogger = new Mock<ILogger<ExportRecordService>>();

            _service = new ExportRecordService(
                _mockExportRecordRepository.Object,
                _mockMediaItemRepository.Object,
                _mockUnitOfWork.Object,
                _mockActionLogger.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task RecordExportAsync_LogsSingleActionLogEntry()
        {
            // Arrange
            var exportedByUserName = "test_user";
            var caseReference = "Case-2024-001";
            var recipientDescription = "Law Enforcement";
            var archiveFileName = "evidence_export.zip";
            var archiveSha256Hash = "abc123def456";
            var wasEncrypted = true;

            var items = new List<(Guid, string)>
            {
                (Guid.NewGuid(), "hash1"),
                (Guid.NewGuid(), "hash2")
            };

            var mockContext = new Mock<IUnitOfWorkContext>();
            var mockExportRecordRepoInContext = new Mock<IExportRecordRepository>();
            var mockActionLogRepoInContext = new Mock<IActionLogRepository>();

            mockContext.Setup(x => x.ExportRecords).Returns(mockExportRecordRepoInContext.Object);
            mockContext.Setup(x => x.ActionLog).Returns(mockActionLogRepoInContext.Object);

            var expectedLogEntry = new ActionLogEntry
            {
                Id = Guid.NewGuid(),
                Actor = exportedByUserName,
                ActorType = ActorType.Human,
                Action = "EvidenceExported",
                EntityType = "ExportRecord",
                TimestampUtc = DateTime.UtcNow
            ,
                EntryHash = "test_hash"};

            mockExportRecordRepoInContext
                .Setup(x => x.AppendAsync(It.IsAny<ExportRecord>(), It.IsAny<IReadOnlyList<ExportRecordItem>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ExportRecord record, IReadOnlyList<ExportRecordItem> items, CancellationToken ct) => record);

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
                    It.IsAny<Func<IUnitOfWorkContext, Task<ExportRecord>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<ExportRecord>> work, CancellationToken ct) =>
                    await work(mockContext.Object));

            // Act
            var result = await _service.RecordExportAsync(
                exportedByUserName,
                caseReference,
                recipientDescription,
                archiveFileName,
                archiveSha256Hash,
                wasEncrypted,
                items,
                CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(items.Count, result.ItemCount);
            Assert.Equal(archiveFileName, result.ArchiveFileName);
            Assert.Equal(archiveSha256Hash, result.ArchiveSha256Hash);
            Assert.True(result.WasEncrypted);
            Assert.Equal(caseReference, result.CaseReference);

            // Verify exactly one action log entry was created
            mockActionLogRepoInContext.Verify(
                x => x.AppendAsync(
                    exportedByUserName,
                    ActorType.Human,
                    "EvidenceExported",
                    "ExportRecord",
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task RecordExportAsync_CreatesExportRecordWithCorrectValues()
        {
            // Arrange
            var exportedByUserName = "analyst_123";
            var caseReference = "Case-2024-002";
            var recipientDescription = "District Attorney";
            var archiveFileName = "evidence_2024.zip";
            var archiveSha256Hash = "xyz789";
            var wasEncrypted = false;

            var items = new List<(Guid, string)>
            {
                (Guid.NewGuid(), "item_hash_1")
            };

            var mockContext = new Mock<IUnitOfWorkContext>();
            var mockExportRecordRepoInContext = new Mock<IExportRecordRepository>();
            var mockActionLogRepoInContext = new Mock<IActionLogRepository>();

            mockContext.Setup(x => x.ExportRecords).Returns(mockExportRecordRepoInContext.Object);
            mockContext.Setup(x => x.ActionLog).Returns(mockActionLogRepoInContext.Object);

            ExportRecord? capturedRecord = null;
            mockExportRecordRepoInContext
                .Setup(x => x.AppendAsync(It.IsAny<ExportRecord>(), It.IsAny<IReadOnlyList<ExportRecordItem>>(), It.IsAny<CancellationToken>()))
                .Callback<ExportRecord, IReadOnlyList<ExportRecordItem>, CancellationToken>((record, items, ct) => { capturedRecord = record; })
                .ReturnsAsync((ExportRecord record, IReadOnlyList<ExportRecordItem> items, CancellationToken ct) => record);

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
                    It.IsAny<Func<IUnitOfWorkContext, Task<ExportRecord>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<ExportRecord>> work, CancellationToken ct) =>
                    await work(mockContext.Object));

            // Act
            await _service.RecordExportAsync(
                exportedByUserName,
                caseReference,
                recipientDescription,
                archiveFileName,
                archiveSha256Hash,
                wasEncrypted,
                items,
                CancellationToken.None);

            // Assert
            Assert.NotNull(capturedRecord);
            Assert.Equal(exportedByUserName, capturedRecord.ExportedByUserName);
            Assert.Equal(caseReference, capturedRecord.CaseReference);
            Assert.Equal(recipientDescription, capturedRecord.RecipientDescription);
            Assert.Equal(archiveFileName, capturedRecord.ArchiveFileName);
            Assert.Equal(archiveSha256Hash, capturedRecord.ArchiveSha256Hash);
            Assert.False(capturedRecord.WasEncrypted);
            Assert.Equal(1, capturedRecord.ItemCount);
            Assert.NotNull(capturedRecord.AppVersion);
        }

        [Fact]
        public async Task RecordExportAsync_WithNullCaseReferenceAndRecipient_StillLogsEntry()
        {
            // Arrange
            var exportedByUserName = "analyst";
            var archiveFileName = "export.zip";
            var archiveSha256Hash = "hash123";
            var items = new List<(Guid, string)> { (Guid.NewGuid(), "hash") };

            var mockContext = new Mock<IUnitOfWorkContext>();
            var mockExportRecordRepoInContext = new Mock<IExportRecordRepository>();
            var mockActionLogRepoInContext = new Mock<IActionLogRepository>();

            mockContext.Setup(x => x.ExportRecords).Returns(mockExportRecordRepoInContext.Object);
            mockContext.Setup(x => x.ActionLog).Returns(mockActionLogRepoInContext.Object);

            mockExportRecordRepoInContext
                .Setup(x => x.AppendAsync(It.IsAny<ExportRecord>(), It.IsAny<IReadOnlyList<ExportRecordItem>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ExportRecord record, IReadOnlyList<ExportRecordItem> items, CancellationToken ct) => record);

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
                    It.IsAny<Func<IUnitOfWorkContext, Task<ExportRecord>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<ExportRecord>> work, CancellationToken ct) =>
                    await work(mockContext.Object));

            // Act
            var result = await _service.RecordExportAsync(
                exportedByUserName,
                null,
                null,
                archiveFileName,
                archiveSha256Hash,
                false,
                items,
                CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Null(result.CaseReference);
            Assert.Null(result.RecipientDescription);

            mockActionLogRepoInContext.Verify(
                x => x.AppendAsync(
                    It.IsAny<string>(),
                    It.IsAny<ActorType>(),
                    "EvidenceExported",
                    "ExportRecord",
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task RecordExportAsync_CreatesExportRecordItemsWithCorrectHashes()
        {
            // Arrange
            var items = new List<(Guid, string)>
            {
                (Guid.NewGuid(), "hash_item_1"),
                (Guid.NewGuid(), "hash_item_2"),
                (Guid.NewGuid(), "hash_item_3")
            };

            var mockContext = new Mock<IUnitOfWorkContext>();
            var mockExportRecordRepoInContext = new Mock<IExportRecordRepository>();
            var mockActionLogRepoInContext = new Mock<IActionLogRepository>();

            mockContext.Setup(x => x.ExportRecords).Returns(mockExportRecordRepoInContext.Object);
            mockContext.Setup(x => x.ActionLog).Returns(mockActionLogRepoInContext.Object);

            IReadOnlyList<ExportRecordItem>? capturedItems = null;
            mockExportRecordRepoInContext
                .Setup(x => x.AppendAsync(It.IsAny<ExportRecord>(), It.IsAny<IReadOnlyList<ExportRecordItem>>(), It.IsAny<CancellationToken>()))
                .Callback<ExportRecord, IReadOnlyList<ExportRecordItem>, CancellationToken>((record, items, ct) => { capturedItems = items; })
                .ReturnsAsync((ExportRecord record, IReadOnlyList<ExportRecordItem> items, CancellationToken ct) => record);

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
                    It.IsAny<Func<IUnitOfWorkContext, Task<ExportRecord>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<ExportRecord>> work, CancellationToken ct) =>
                    await work(mockContext.Object));

            // Act
            await _service.RecordExportAsync(
                "user",
                null,
                null,
                "export.zip",
                "archive_hash",
                false,
                items,
                CancellationToken.None);

            // Assert
            Assert.NotNull(capturedItems);
            Assert.Equal(3, capturedItems.Count);

            for (int i = 0; i < items.Count; i++)
            {
                Assert.Equal(items[i].Item1, capturedItems[i].MediaItemId);
                Assert.Equal(items[i].Item2, capturedItems[i].MediaItemSha256HashAtExport);
            }
        }

        [Fact]
        public async Task GetHistoryForMediaItemAsync_ReturnsExportRecords()
        {
            // Arrange
            var mediaItemId = Guid.NewGuid();
            var expectedRecords = new List<ExportRecord>
            {
                new ExportRecord
                {
                    Id = Guid.NewGuid(),
                    ExportedAtUtc = DateTime.UtcNow.AddHours(-2),
                    ExportedByUserName = "user1",
                    ItemCount = 1,
                    ArchiveFileName = "export1.zip"
                ,
                ArchiveSha256Hash = "test_hash",
                AppVersion = "1.0"},
                new ExportRecord
                {
                    Id = Guid.NewGuid(),
                    ExportedAtUtc = DateTime.UtcNow.AddHours(-1),
                    ExportedByUserName = "user2",
                    ItemCount = 2,
                    ArchiveFileName = "export2.zip"
                ,
                ArchiveSha256Hash = "test_hash",
                AppVersion = "1.0"}
            };

            _mockExportRecordRepository
                .Setup(x => x.GetHistoryForMediaItemAsync(mediaItemId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedRecords);

            // Act
            var result = await _service.GetHistoryForMediaItemAsync(mediaItemId, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal(expectedRecords[0].Id, result[0].Id);
            Assert.Equal(expectedRecords[1].Id, result[1].Id);
        }

        [Fact]
        public async Task GetHistoryForDeviceAsync_ReturnsExportRecords()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var expectedRecords = new List<ExportRecord>
            {
                new ExportRecord
                {
                    Id = Guid.NewGuid(),
                    ExportedAtUtc = DateTime.UtcNow,
                    ExportedByUserName = "analyst",
                    ItemCount = 5,
                    ArchiveFileName = "device_export.zip"
                ,
                ArchiveSha256Hash = "test_hash",
                AppVersion = "1.0"}
            };

            _mockExportRecordRepository
                .Setup(x => x.GetHistoryForDeviceAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedRecords);

            // Act
            var result = await _service.GetHistoryForDeviceAsync(deviceId, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(expectedRecords[0].Id, result[0].Id);
            Assert.Equal(5, result[0].ItemCount);
        }

        [Fact]
        public async Task GetHistoryForMediaItemAsync_WithNoHistory_ReturnsEmptyList()
        {
            // Arrange
            var mediaItemId = Guid.NewGuid();

            _mockExportRecordRepository
                .Setup(x => x.GetHistoryForMediaItemAsync(mediaItemId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ExportRecord>());

            // Act
            var result = await _service.GetHistoryForMediaItemAsync(mediaItemId, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }
    }
}
