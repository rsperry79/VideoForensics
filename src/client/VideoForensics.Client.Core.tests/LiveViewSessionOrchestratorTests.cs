using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Core.Tools;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Forensics;
using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Providers.Ring.Interfaces;

using Xunit;

using Device = VideoForensics.Data.Common.Entities.Device;

namespace VideoForensics.Client.Core.Tests
{
    public class LiveViewSessionOrchestratorTests
    {
        private readonly Mock<ILogger<LiveViewSessionOrchestrator>> _loggerMock;
        private readonly Mock<ILiveViewSessionRepository> _sessionRepositoryMock;
        private readonly Mock<ILiveViewInterferenceScorer> _scorerMock;
        private readonly Mock<IDeviceRepository> _deviceRepositoryMock;
        private readonly Mock<IForensicsConfiguration> _configMock;
        private readonly Mock<IProviderApiBudgetGuard> _budgetGuardMock;
        private readonly Mock<INotificationDispatcher> _notificationDispatcherMock;

        public LiveViewSessionOrchestratorTests()
        {
            _loggerMock = new Mock<ILogger<LiveViewSessionOrchestrator>>();
            _sessionRepositoryMock = new Mock<ILiveViewSessionRepository>();
            _scorerMock = new Mock<ILiveViewInterferenceScorer>();
            _deviceRepositoryMock = new Mock<IDeviceRepository>();
            _configMock = new Mock<IForensicsConfiguration>();
            _budgetGuardMock = new Mock<IProviderApiBudgetGuard>();
            _notificationDispatcherMock = new Mock<INotificationDispatcher>();

            // Default config: live view enabled
            _configMock.Setup(c => c.EnableLiveView).Returns(true);
            _configMock.Setup(c => c.LiveViewIdleTimeoutMinutes).Returns(5);
            _configMock.Setup(c => c.LiveViewTelemetrySampleIntervalSeconds).Returns(2);
            _configMock.Setup(c => c.SustainedModeMaxDurationMinutes).Returns(240);
        }

        private LiveViewSessionOrchestrator CreateOrchestrator(Mock<IServiceProvider>? serviceProviderMock = null)
        {
            // The orchestrator opens a DI scope per operation. Wire the scope factory to a scope whose
            // provider resolves the repository and service mocks, mirroring the production registration.
            var sp = serviceProviderMock ?? new Mock<IServiceProvider>();
            sp.Setup(s => s.GetService(typeof(ILiveViewSessionRepository))).Returns(_sessionRepositoryMock.Object);
            sp.Setup(s => s.GetService(typeof(IDeviceRepository))).Returns(_deviceRepositoryMock.Object);
            sp.Setup(s => s.GetService(typeof(IProviderApiBudgetGuard))).Returns(_budgetGuardMock.Object);
            sp.Setup(s => s.GetService(typeof(INotificationDispatcher))).Returns(_notificationDispatcherMock.Object);

            var scopeMock = new Mock<IServiceScope>();
            scopeMock.Setup(s => s.ServiceProvider).Returns(sp.Object);

            var scopeFactoryMock = new Mock<IServiceScopeFactory>();
            scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

            return new LiveViewSessionOrchestrator(
                _loggerMock.Object,
                scopeFactoryMock.Object,
                _scorerMock.Object,
                _configMock.Object);
        }

        [Fact]
        public async Task StartAsync_NoActiveSession_CreatesAndPersistsStartingSession()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var operatorId = Guid.NewGuid();
            var device = new Device
            {
                Id = deviceId,
                ProviderDeviceId = "ring-device-123",
                LocationId = Guid.NewGuid(),
                Name = "Test Camera",
                Type = "camera"
            };

            var mockConnection = new Mock<ILiveViewConnection>();
            var mockProvider = new Mock<ILiveViewCapableProvider>();
            mockProvider.Setup(p => p.StartLiveViewAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockConnection.Object);

            _deviceRepositoryMock.Setup(r => r.GetAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(device);
            _sessionRepositoryMock.Setup(r => r.GetActiveForDeviceAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((LiveViewSession?)null);
            _budgetGuardMock.Setup(b => b.TryConsumeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            _sessionRepositoryMock.Setup(r => r.UpsertSessionAsync(It.IsAny<LiveViewSession>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((LiveViewSession s, CancellationToken _) => s);

            var spMock = new Mock<IServiceProvider>();
            spMock.Setup(s => s.GetService(typeof(ILiveViewCapableProvider)))
                .Returns(mockProvider.Object);

            var orchestrator = CreateOrchestrator(spMock);

            // Act
            var result = await orchestrator.StartAsync(deviceId, LiveViewTriggerReason.Manual, operatorId, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(deviceId, result.DeviceId);
            Assert.Equal(LiveViewTriggerReason.Manual, result.TriggerReason);
            Assert.Equal(operatorId, result.OperatorId);
            Assert.True(result.State == LiveViewSessionState.Starting || result.State == LiveViewSessionState.Active);
            _sessionRepositoryMock.Verify(r => r.UpsertSessionAsync(It.IsAny<LiveViewSession>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task StartAsync_ExistingActiveSession_ReturnsExistingWithoutStartingSecondConnection()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var existingSessionId = Guid.NewGuid();
            var existingSession = new LiveViewSession
            {
                Id = existingSessionId,
                DeviceId = deviceId,
                State = LiveViewSessionState.Active,
                TriggerReason = LiveViewTriggerReason.Manual
            };

            var device = new Device
            {
                Id = deviceId,
                ProviderDeviceId = "ring-device-123",
                LocationId = Guid.NewGuid(),
                Name = "Test",
                Type = "camera"
            };
            var mockProvider = new Mock<ILiveViewCapableProvider>();

            _deviceRepositoryMock.Setup(r => r.GetAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(device);
            _sessionRepositoryMock.Setup(r => r.GetActiveForDeviceAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingSession);

            var spMock = new Mock<IServiceProvider>();
            var orchestrator = CreateOrchestrator(spMock);

            // Act
            var result = await orchestrator.StartAsync(deviceId, LiveViewTriggerReason.Manual, null, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(existingSessionId, result.Id);
            mockProvider.Verify(p => p.StartLiveViewAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task StartAsync_LiveViewDisabled_ThrowsInvalidOperationException()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            _configMock.Setup(c => c.EnableLiveView).Returns(false);

            var orchestrator = CreateOrchestrator();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => orchestrator.StartAsync(deviceId, LiveViewTriggerReason.Manual, null, CancellationToken.None));
            Assert.Contains("disabled", ex.Message);
        }

        [Fact]
        public async Task StartAsync_BudgetExceeded_DoesNotCallProvider()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var device = new Device
            {
                Id = deviceId,
                ProviderDeviceId = "ring-device-123",
                LocationId = Guid.NewGuid(),
                Name = "Test",
                Type = "camera"
            };
            var mockProvider = new Mock<ILiveViewCapableProvider>();

            _deviceRepositoryMock.Setup(r => r.GetAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(device);
            _sessionRepositoryMock.Setup(r => r.GetActiveForDeviceAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((LiveViewSession?)null);
            _budgetGuardMock.Setup(b => b.TryConsumeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var spMock = new Mock<IServiceProvider>();
            spMock.Setup(s => s.GetService(typeof(ILiveViewCapableProvider)))
                .Returns(mockProvider.Object);

            var orchestrator = CreateOrchestrator(spMock);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => orchestrator.StartAsync(deviceId, LiveViewTriggerReason.Manual, null, CancellationToken.None));
            mockProvider.Verify(p => p.StartLiveViewAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task StartAsync_NoProviderRegisteredForDevice_ThrowsNotSupported()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var device = new Device
            {
                Id = deviceId,
                ProviderDeviceId = "device-123",
                LocationId = Guid.NewGuid(),
                Name = "Test",
                Type = "camera"
            };

            _deviceRepositoryMock.Setup(r => r.GetAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(device);
            _sessionRepositoryMock.Setup(r => r.GetActiveForDeviceAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((LiveViewSession?)null);
            _budgetGuardMock.Setup(b => b.TryConsumeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var spMock = new Mock<IServiceProvider>();
            spMock.Setup(s => s.GetService(typeof(ILiveViewCapableProvider)))
                .Returns(null);

            var orchestrator = CreateOrchestrator(spMock);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<NotSupportedException>(
                () => orchestrator.StartAsync(deviceId, LiveViewTriggerReason.Manual, null, CancellationToken.None));
        }

        [Fact]
        public async Task ExtendAsync_ActiveSession_UpdatesLastExtendedAtUtc()
        {
            // Arrange
            var sessionId = Guid.NewGuid();
            var session = new LiveViewSession { Id = sessionId, State = LiveViewSessionState.Active };

            LiveViewSession? capturedSession = null;
            _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(session);
            _sessionRepositoryMock.Setup(r => r.UpsertSessionAsync(It.IsAny<LiveViewSession>(), It.IsAny<CancellationToken>()))
                .Callback<LiveViewSession, CancellationToken>((s, _) => capturedSession = s)
                .ReturnsAsync((LiveViewSession s, CancellationToken _) => s);

            var orchestrator = CreateOrchestrator();
            var beforeExtend = DateTime.UtcNow;

            // Act
            await orchestrator.ExtendAsync(sessionId, CancellationToken.None);

            // Assert
            Assert.NotNull(capturedSession);
            Assert.True(capturedSession.LastExtendedAtUtc >= beforeExtend);
            _sessionRepositoryMock.Verify(r => r.UpsertSessionAsync(It.IsAny<LiveViewSession>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExtendAsync_UnknownSessionId_ThrowsKeyNotFoundException()
        {
            // Arrange
            var sessionId = Guid.NewGuid();
            _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((LiveViewSession?)null);

            var orchestrator = CreateOrchestrator();

            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(
                () => orchestrator.ExtendAsync(sessionId, CancellationToken.None));
        }

        [Fact]
        public async Task PromoteToSustainedAsync_ActiveSession_SetsIsSustainedAndDispatchesNotification()
        {
            // Arrange
            var sessionId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var operatorId = Guid.NewGuid();
            var session = new LiveViewSession
            {
                Id = sessionId,
                DeviceId = deviceId,
                State = LiveViewSessionState.Active,
                IsSustained = false,
                OperatorId = operatorId
            };

            _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(session);
            _sessionRepositoryMock.Setup(r => r.UpsertSessionAsync(It.IsAny<LiveViewSession>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((LiveViewSession s, CancellationToken _) => s);
            _notificationDispatcherMock.Setup(n => n.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()))
                .Callback<NotificationEvent, CancellationToken>((ne, ct) => { })
                .Returns(Task.CompletedTask);

            var orchestrator = CreateOrchestrator();

            // Act
            var result = await orchestrator.PromoteToSustainedAsync(sessionId, "Test promotion reason", CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsSustained);
            Assert.Equal(LiveViewSessionState.Sustained, result.State);
            Assert.NotNull(result.SustainedSinceUtc);
            Assert.Equal("Test promotion reason", result.PromotionReason);
            _notificationDispatcherMock.Verify(
                n => n.DispatchAsync(It.Is<NotificationEvent>(ne => ne.EventType == "LiveViewSustainedModeEntered"), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task PromoteToSustainedAsync_AlreadySustained_IsIdempotent()
        {
            // Arrange
            var sessionId = Guid.NewGuid();
            var session = new LiveViewSession
            {
                Id = sessionId,
                State = LiveViewSessionState.Sustained,
                IsSustained = true,
                SustainedSinceUtc = DateTime.UtcNow.AddMinutes(-10)
            };

            _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(session);
            _sessionRepositoryMock.Setup(r => r.UpsertSessionAsync(It.IsAny<LiveViewSession>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((LiveViewSession s, CancellationToken _) => s);

            var orchestrator = CreateOrchestrator();

            // Act
            var result = await orchestrator.PromoteToSustainedAsync(sessionId, "Another reason", CancellationToken.None);

            // Assert
            Assert.True(result.IsSustained);
            Assert.Equal(session.SustainedSinceUtc, result.SustainedSinceUtc);
        }

        [Fact]
        public async Task PromoteToSustainedAsync_NotificationDispatchThrows_StillPromotes()
        {
            // Arrange
            var sessionId = Guid.NewGuid();
            var session = new LiveViewSession
            {
                Id = sessionId,
                State = LiveViewSessionState.Active,
                IsSustained = false,
                OperatorId = Guid.NewGuid()
            };

            _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(session);
            _sessionRepositoryMock.Setup(r => r.UpsertSessionAsync(It.IsAny<LiveViewSession>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((LiveViewSession s, CancellationToken _) => s);
            _notificationDispatcherMock.Setup(n => n.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()))
                .Callback<NotificationEvent, CancellationToken>((ne, ct) => { throw new Exception("Dispatch failed"); })
                .Returns(Task.CompletedTask);

            var orchestrator = CreateOrchestrator();

            // Act
            var result = await orchestrator.PromoteToSustainedAsync(sessionId, "Test", CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsSustained);
            _notificationDispatcherMock.Verify(n => n.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DemoteFromSustainedAsync_SustainedSession_ClearsSustainedState()
        {
            // Arrange
            var sessionId = Guid.NewGuid();
            var session = new LiveViewSession
            {
                Id = sessionId,
                State = LiveViewSessionState.Sustained,
                IsSustained = true,
                SustainedSinceUtc = DateTime.UtcNow.AddMinutes(-10)
            };

            _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(session);
            _sessionRepositoryMock.Setup(r => r.UpsertSessionAsync(It.IsAny<LiveViewSession>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((LiveViewSession s, CancellationToken _) => s);

            var orchestrator = CreateOrchestrator();

            // Act
            var result = await orchestrator.DemoteFromSustainedAsync(sessionId, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsSustained);
            Assert.Null(result.SustainedSinceUtc);
            Assert.Equal(LiveViewSessionState.Active, result.State);
            _sessionRepositoryMock.Verify(r => r.UpsertSessionAsync(It.IsAny<LiveViewSession>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task StopAsync_ActiveSession_ClosesConnectionAndPersistsStoppedState()
        {
            // Arrange
            var sessionId = Guid.NewGuid();
            var session = new LiveViewSession
            {
                Id = sessionId,
                State = LiveViewSessionState.Active
            };

            _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(session);
            _sessionRepositoryMock.Setup(r => r.UpsertSessionAsync(It.IsAny<LiveViewSession>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((LiveViewSession s, CancellationToken _) => s);

            var orchestrator = CreateOrchestrator();

            // Act
            await orchestrator.StopAsync(sessionId, "ManualStop", CancellationToken.None);

            // Assert
            _sessionRepositoryMock.Verify(r => r.UpsertSessionAsync(It.IsAny<LiveViewSession>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task StopAsync_AlreadyStopped_IsIdempotent()
        {
            // Arrange
            var sessionId = Guid.NewGuid();
            var session = new LiveViewSession
            {
                Id = sessionId,
                State = LiveViewSessionState.Stopped,
                EndedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            };

            _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(session);

            var orchestrator = CreateOrchestrator();

            // Act
            await orchestrator.StopAsync(sessionId, "ManualStop", CancellationToken.None);

            // Assert
            _sessionRepositoryMock.Verify(r => r.UpsertSessionAsync(It.IsAny<LiveViewSession>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetActiveSessionAsync_DeviceWithNoSession_ReturnsNull()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            _sessionRepositoryMock.Setup(r => r.GetActiveForDeviceAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((LiveViewSession?)null);

            var orchestrator = CreateOrchestrator();

            // Act
            var result = await orchestrator.GetActiveSessionAsync(deviceId, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetActiveSessionAsync_DelegatesToRepository()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var session = new LiveViewSession { Id = Guid.NewGuid(), DeviceId = deviceId, State = LiveViewSessionState.Active };
            _sessionRepositoryMock.Setup(r => r.GetActiveForDeviceAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(session);

            var orchestrator = CreateOrchestrator();

            // Act
            var result = await orchestrator.GetActiveSessionAsync(deviceId, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(session.Id, result.Id);
            Assert.Equal(session.State, result.State);
            _sessionRepositoryMock.Verify(r => r.GetActiveForDeviceAsync(deviceId, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
