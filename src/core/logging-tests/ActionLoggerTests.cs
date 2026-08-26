using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using VideoForensics.Core.Logging.Services;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using Xunit;

namespace VideoForensics.Core.Logging.Tests
{
    public class ActionLoggerTests
    {
        private readonly Mock<IActionLogRepository> _mockActionLogRepository;
        private readonly Mock<ILogger<ActionLogger>> _mockLogger;
        private readonly ActionLogger _actionLogger;

        public ActionLoggerTests()
        {
            _mockActionLogRepository = new Mock<IActionLogRepository>();
            _mockLogger = new Mock<ILogger<ActionLogger>>();
            _actionLogger = new ActionLogger(_mockActionLogRepository.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task LogAsync_ForwardsToRepositoryWithEnvironmentUserName()
        {
            // Arrange
            var action = "TestAction";
            var entityType = "TestEntity";
            var entityId = Guid.NewGuid();
            var details = "test details";
            var userName = Environment.UserName;

            var expectedEntry = TestHelpers.CreateActionLogEntry(
                actor: userName,
                action: action,
                entityType: entityType,
                entityId: entityId,
                details: details);

            _mockActionLogRepository
                .Setup(x => x.AppendAsync(
                    userName,
                    ActorType.Human,
                    action,
                    entityType,
                    entityId,
                    details,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedEntry);

            // Act
            var result = await _actionLogger.LogAsync(action, entityType, entityId, details, CancellationToken.None);

            // Assert
            Assert.Equal(expectedEntry, result);
            _mockActionLogRepository.Verify(
                x => x.AppendAsync(
                    userName,
                    ActorType.Human,
                    action,
                    entityType,
                    entityId,
                    details,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task LogAsync_WithoutDetails_ForwardsWithNullDetails()
        {
            // Arrange
            var action = "TestAction";
            var entityType = "TestEntity";
            var entityId = Guid.NewGuid();
            var userName = Environment.UserName;

            var expectedEntry = new ActionLogEntry
            {
                Id = Guid.NewGuid(),
                Actor = userName,
                ActorType = ActorType.Human,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                DetailsJson = null,
                TimestampUtc = DateTime.UtcNow,
                EntryHash = "test_hash"
            };

            _mockActionLogRepository
                .Setup(x => x.AppendAsync(
                    userName,
                    ActorType.Human,
                    action,
                    entityType,
                    entityId,
                    null,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedEntry);

            // Act
            var result = await _actionLogger.LogAsync(action, entityType, entityId, null, CancellationToken.None);

            // Assert
            Assert.Null(result.DetailsJson);
            _mockActionLogRepository.Verify(
                x => x.AppendAsync(
                    userName,
                    ActorType.Human,
                    action,
                    entityType,
                    entityId,
                    null,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task LogAsAsync_WithCustomActorAndType_ForwardsToRepository()
        {
            // Arrange
            var customActor = "mcp:tool-name";
            var customActorType = ActorType.McpTool;
            var action = "AnalysisPerformed";
            var entityType = "MediaItem";
            var entityId = Guid.NewGuid();
            var details = "analysis details";

            var expectedEntry = new ActionLogEntry
            {
                Id = Guid.NewGuid(),
                Actor = customActor,
                ActorType = customActorType,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                DetailsJson = details,
                TimestampUtc = DateTime.UtcNow,
                EntryHash = "test_hash"
            };

            _mockActionLogRepository
                .Setup(x => x.AppendAsync(
                    customActor,
                    customActorType,
                    action,
                    entityType,
                    entityId,
                    details,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedEntry);

            // Act
            var result = await _actionLogger.LogAsAsync(customActor, customActorType, action, entityType, entityId, details, CancellationToken.None);

            // Assert
            Assert.Equal(customActor, result.Actor);
            Assert.Equal(customActorType, result.ActorType);
            _mockActionLogRepository.Verify(
                x => x.AppendAsync(
                    customActor,
                    customActorType,
                    action,
                    entityType,
                    entityId,
                    details,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task LogAsAsync_WithSystemActorType_ForwardsWithSystemType()
        {
            // Arrange
            var action = "RetentionPurge";
            var entityType = "MediaItem";
            var entityId = Guid.NewGuid();

            var expectedEntry = new ActionLogEntry
            {
                Id = Guid.NewGuid(),
                Actor = "system",
                ActorType = ActorType.System,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                TimestampUtc = DateTime.UtcNow,
                EntryHash = "test_hash"
            };

            _mockActionLogRepository
                .Setup(x => x.AppendAsync(
                    "system",
                    ActorType.System,
                    action,
                    entityType,
                    entityId,
                    null,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedEntry);

            // Act
            var result = await _actionLogger.LogAsAsync("system", ActorType.System, action, entityType, entityId, null, CancellationToken.None);

            // Assert
            Assert.Equal(ActorType.System, result.ActorType);
        }

        [Fact]
        public async Task LogAsync_DoesNotCallRepositoryMultipleTimes()
        {
            // Arrange
            var action = "TestAction";
            var entityType = "TestEntity";

            var expectedEntry = new ActionLogEntry
            {
                Id = Guid.NewGuid(),
                Actor = Environment.UserName,
                ActorType = ActorType.Human,
                Action = action,
                EntityType = entityType,
                TimestampUtc = DateTime.UtcNow,
                EntryHash = "test_hash"
            };

            _mockActionLogRepository
                .Setup(x => x.AppendAsync(
                    It.IsAny<string>(),
                    It.IsAny<ActorType>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedEntry);

            // Act
            await _actionLogger.LogAsync(action, entityType, null, null, CancellationToken.None);

            // Assert - verify AppendAsync is called exactly once
            _mockActionLogRepository.Verify(
                x => x.AppendAsync(
                    It.IsAny<string>(),
                    It.IsAny<ActorType>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
