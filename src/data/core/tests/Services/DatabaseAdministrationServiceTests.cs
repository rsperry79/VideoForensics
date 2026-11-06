using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Services;

using Xunit;

namespace VideoForensics.Data.Core.Tests
{
    public class DatabaseAdministrationServiceTests
    {
        private readonly Mock<IDatabaseEngine> _engine = new();
        private readonly Mock<IMediaItemRepository> _mediaItems = new();
        private readonly Mock<ILegalHoldRepository> _legalHolds = new();
        private readonly Mock<IUnitOfWork> _unitOfWork = new();
        private readonly Mock<IActionLogRepository> _actionLog = new();
        private readonly Mock<ILogger<DatabaseAdministrationService>> _logger = new();

        private DatabaseAdministrationService CreateService()
        {
            return new DatabaseAdministrationService(
                _engine.Object,
                _mediaItems.Object,
                _legalHolds.Object,
                _unitOfWork.Object,
                _actionLog.Object,
                _logger.Object);
        }

        public DatabaseAdministrationServiceTests()
        {
            _ = _legalHolds
                .Setup(x => x.GetActiveByMediaItemIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
            _ = _actionLog
                .Setup(x => x.VerifyChainIntegrityAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(ChainIntegrityResult.Valid(0));
        }

        [Fact]
        public async Task ValidateAsync_MissingFileOnDisk_ListsRowAsMissing()
        {
            // Arrange
            var missing = new MediaItem { MediaFormat = "video/mp4", Sha256Hash = "abc", Id = Guid.NewGuid(), FileName = "gone.mp4", FilePath = "ring/gone.mp4" };
            _ = _mediaItems.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([missing]);
            DatabaseAdministrationService service = CreateService();

            // Act
            DatabaseValidationResult result = await service.ValidateAsync(CancellationToken.None);

            // Assert
            Assert.Single(result.MissingMedia);
            Assert.Equal(missing.Id, result.MissingMedia[0].MediaItemId);
            Assert.False(result.IsClean);
        }

        [Fact]
        public async Task ValidateAsync_PurgedRowWithoutFile_IsNotListed()
        {
            // Arrange
            var purged = new MediaItem { MediaFormat = "video/mp4", Sha256Hash = "abc", Id = Guid.NewGuid(), FileName = "gone.mp4", FilePath = "ring/gone.mp4", IsPurged = true };
            _ = _mediaItems.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([purged]);
            DatabaseAdministrationService service = CreateService();

            // Act
            DatabaseValidationResult result = await service.ValidateAsync(CancellationToken.None);

            // Assert
            Assert.Empty(result.MissingMedia);
            Assert.True(result.IsClean);
        }

        [Fact]
        public async Task PurgeMissingMediaAsync_RowUnderLegalHold_IsNotPurged()
        {
            // Arrange
            var held = new MediaItem { MediaFormat = "video/mp4", Sha256Hash = "abc", Id = Guid.NewGuid(), FileName = "held.mp4", FilePath = "ring/held.mp4" };
            _ = _legalHolds
                .Setup(x => x.GetActiveByMediaItemIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([new LegalHold { CreatedBy = "test", Reason = "test", MediaItemId = held.Id }]);
            DatabaseAdministrationService service = CreateService();

            // Act
            int purged = await service.PurgeMissingMediaAsync([held.Id], CancellationToken.None);

            // Assert
            Assert.Equal(0, purged);
            _unitOfWork.Verify(x => x.ExecuteAsync(It.IsAny<Func<IUnitOfWorkContext, Task<bool>>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RepairChainAsync_EntryContentsAltered_IsReportedNotBridged()
        {
            // Arrange
            var altered = new ActionLogEntry { Action = "x", Actor = "test", EntityType = "MediaItem", Id = Guid.NewGuid(), TimestampUtc = DateTime.UtcNow, EntryHash = "h" };
            _ = _actionLog
                .Setup(x => x.VerifyChainIntegrityAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(ChainIntegrityResult.Broken(0, ChainBreakKind.EntryHashMismatch, altered));
            DatabaseAdministrationService service = CreateService();

            // Act
            ChainRepairResult result = await service.RepairChainAsync(CancellationToken.None);

            // Assert
            Assert.Equal(0, result.BridgesAdded);
            Assert.NotNull(result.Unrepairable);
            _actionLog.Verify(x => x.AppendAsync(It.IsAny<string>(), It.IsAny<ActorType>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RepairChainAsync_MissingLink_AppendsBridgeFromLastVerifiedHash()
        {
            // Arrange
            var previous = new ActionLogEntry { Action = "x", Actor = "test", EntityType = "MediaItem", Id = Guid.NewGuid(), TimestampUtc = DateTime.UtcNow.AddMinutes(-2), EntryHash = "hash-before-gap" };
            var broken = new ActionLogEntry { Action = "x", Actor = "test", EntityType = "MediaItem", Id = Guid.NewGuid(), TimestampUtc = DateTime.UtcNow.AddMinutes(-1), PreviousEntryHash = "hash-of-removed", EntryHash = "x" };
            _ = _actionLog
                .SetupSequence(x => x.VerifyChainIntegrityAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(ChainIntegrityResult.Broken(1, ChainBreakKind.PreviousHashMismatch, broken))
                .ReturnsAsync(ChainIntegrityResult.Valid(3));
            _ = _actionLog
                .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([previous, broken]);
            DatabaseAdministrationService service = CreateService();

            // Act
            ChainRepairResult result = await service.RepairChainAsync(CancellationToken.None);

            // Assert
            Assert.Equal(1, result.BridgesAdded);
            Assert.Null(result.Unrepairable);
            _actionLog.Verify(x => x.AppendAsync(
                It.IsAny<string>(),
                It.IsAny<ActorType>(),
                ChainRepairRecord.Action,
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.Is<string?>(d => d != null && d.Contains("hash-before-gap") && d.Contains("hash-of-removed")),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
