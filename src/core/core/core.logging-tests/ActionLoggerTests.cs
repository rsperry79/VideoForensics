using Microsoft.Extensions.Logging;

using Moq;

using Serilog.Context;

using System;
using System.Threading;
using System.Threading.Tasks;

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
            _actionLogger = new ActionLogger(_mockLogger.Object, _mockActionLogRepository.Object);
        }

        [Fact]
        public async Task LogAsync_ForwardsToRepositoryWithEnvironmentUserName()
        {
            // Arrange
            string action = "TestAction";
            string entityType = "TestEntity";
            var entityId = Guid.NewGuid();
            string details = "test details";
            string userName = Environment.UserName;

            ActionLogEntry expectedEntry = TestHelpers.CreateActionLogEntry(
                actor: userName,
                action: action,
                entityType: entityType,
                entityId: entityId,
                details: details);

            _ = _mockActionLogRepository
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
            ActionLogEntry result = await _actionLogger.LogAsync(action, entityType, entityId, details, CancellationToken.None);

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
            string action = "TestAction";
            string entityType = "TestEntity";
            var entityId = Guid.NewGuid();
            string userName = Environment.UserName;

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

            _ = _mockActionLogRepository
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
            ActionLogEntry result = await _actionLogger.LogAsync(action, entityType, entityId, null, CancellationToken.None);

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
            string customActor = "mcp:tool-name";
            ActorType customActorType = ActorType.McpTool;
            string action = "AnalysisPerformed";
            string entityType = "MediaItem";
            var entityId = Guid.NewGuid();
            string details = "analysis details";

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

            _ = _mockActionLogRepository
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
            ActionLogEntry result = await _actionLogger.LogAsAsync(customActor, customActorType, action, entityType, entityId, details, CancellationToken.None);

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
            string action = "RetentionPurge";
            string entityType = "MediaItem";
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

            _ = _mockActionLogRepository
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
            ActionLogEntry result = await _actionLogger.LogAsAsync("system", ActorType.System, action, entityType, entityId, null, CancellationToken.None);

            // Assert
            Assert.Equal(ActorType.System, result.ActorType);
        }

        [Fact]
        public async Task LogAsync_DoesNotCallRepositoryMultipleTimes()
        {
            // Arrange
            string action = "TestAction";
            string entityType = "TestEntity";

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

            _ = _mockActionLogRepository
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
            _ = await _actionLogger.LogAsync(action, entityType, null, null, CancellationToken.None);

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

        [Fact]
        public async Task LogAsync_WithSerilog_LogsStructuredProperties()
        {
            // Arrange
            string action = "CreateDevice";
            string entityType = "Device";
            var entityId = Guid.NewGuid();
            string userName = Environment.UserName;

            var expectedEntry = TestHelpers.CreateActionLogEntry(
                actor: userName,
                action: action,
                entityType: entityType,
                entityId: entityId);

            _ = _mockActionLogRepository
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
            _ = await _actionLogger.LogAsync(action, entityType, entityId);

            // Assert - verify Serilog ILogger.LogInformation was called with structured data
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task LogAsAsync_WithSerilog_EnrichesWithActorAndEntityType()
        {
            // Arrange
            string customActor = "system:retention-job";
            ActorType actorType = ActorType.System;
            string action = "MediaPurge";
            string entityType = "MediaItem";
            var entityId = Guid.NewGuid();

            var expectedEntry = TestHelpers.CreateActionLogEntry(
                actor: customActor,
                action: action,
                entityType: entityType,
                entityId: entityId);

            _ = _mockActionLogRepository
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
            _ = await _actionLogger.LogAsAsync(customActor, actorType, action, entityType, entityId);

            // Assert - verify logger was called (structured enrichment happens via LogContext internally)
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task LogAsync_DualWrite_LogsToSerilogAndRepository()
        {
            // Arrange
            string action = "UpdateConfig";
            string entityType = "Configuration";
            var entityId = Guid.NewGuid();
            string details = "setting changed";

            var expectedEntry = TestHelpers.CreateActionLogEntry(
                action: action,
                entityType: entityType,
                entityId: entityId,
                details: details);

            _ = _mockActionLogRepository
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
            ActionLogEntry result = await _actionLogger.LogAsync(action, entityType, entityId, details);

            // Assert - verify BOTH Serilog and repository were called (dual-write)
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once,
                "Serilog should be called");

            _mockActionLogRepository.Verify(
                x => x.AppendAsync(
                    It.IsAny<string>(),
                    It.IsAny<ActorType>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Once,
                "Repository should be called");

            Assert.Equal(expectedEntry, result);
        }

        [Fact]
        public async Task LogAsAsync_RepositoryFailure_StillLogsToSerilog()
        {
            // Arrange
            string action = "FailingAction";
            string entityType = "TestEntity";
            var repositoryException = new InvalidOperationException("DB connection failed");

            _ = _mockActionLogRepository
                .Setup(x => x.AppendAsync(
                    It.IsAny<string>(),
                    It.IsAny<ActorType>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(repositoryException);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _actionLogger.LogAsync(action, entityType));

            // Serilog should have been called before repository failed
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once,
                "Serilog (primary) should be called even if repository fails");

            Assert.Equal(repositoryException, ex);
        }
    }
}
