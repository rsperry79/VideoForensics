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
    /// <summary>
    /// Verifies that the orchestrator publishes session state changes through <see cref="ILiveViewTelemetryPublisher"/>
    /// and that publish failures never break the session lifecycle.
    /// </summary>
    public class LiveViewSessionOrchestratorPublisherTests
    {
        private readonly Mock<ILiveViewSessionRepository> _sessionRepositoryMock;
        private readonly Mock<IDeviceRepository> _deviceRepositoryMock;
        private readonly Mock<IForensicsConfiguration> _configMock;
        private readonly Mock<IProviderApiBudgetGuard> _budgetGuardMock;
        private readonly Mock<INotificationDispatcher> _notificationDispatcherMock;
        private readonly Mock<ILiveViewTelemetryPublisher> _publisherMock;

        public LiveViewSessionOrchestratorPublisherTests()
        {
            _sessionRepositoryMock = new Mock<ILiveViewSessionRepository>();
            _deviceRepositoryMock = new Mock<IDeviceRepository>();
            _configMock = new Mock<IForensicsConfiguration>();
            _budgetGuardMock = new Mock<IProviderApiBudgetGuard>();
            _notificationDispatcherMock = new Mock<INotificationDispatcher>();
            _publisherMock = new Mock<ILiveViewTelemetryPublisher>();

            _configMock.Setup(c => c.EnableLiveView).Returns(true);
            _configMock.Setup(c => c.LiveViewIdleTimeoutMinutes).Returns(5);
            _configMock.Setup(c => c.SustainedModeMaxDurationMinutes).Returns(240);

            _sessionRepositoryMock.Setup(r => r.UpsertSessionAsync(It.IsAny<LiveViewSession>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((LiveViewSession s, CancellationToken _) => s);
        }

        private LiveViewSessionOrchestrator CreateOrchestrator(Mock<IServiceProvider>? serviceProviderMock = null)
        {
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
                Mock.Of<ILogger<LiveViewSessionOrchestrator>>(),
                scopeFactoryMock.Object,
                Mock.Of<ILiveViewInterferenceScorer>(),
                _configMock.Object,
                _publisherMock.Object);
        }

        private static LiveViewSession ActiveSession(Guid sessionId) => new()
        {
            Id = sessionId,
            State = LiveViewSessionState.Active,
            TriggerReason = LiveViewTriggerReason.Manual
        };

        [Fact]
        public async Task StartAsync_NewSession_PublishesSessionChanged()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var device = new Device { Id = deviceId, ProviderDeviceId = "ring-device-123", LocationId = Guid.NewGuid(), Name = "Test", Type = "camera" };
            var mockProvider = new Mock<ILiveViewCapableProvider>();
            mockProvider.Setup(p => p.StartLiveViewAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Mock<ILiveViewConnection>().Object);

            _deviceRepositoryMock.Setup(r => r.GetAsync(deviceId, It.IsAny<CancellationToken>())).ReturnsAsync(device);
            _sessionRepositoryMock.Setup(r => r.GetActiveForDeviceAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((LiveViewSession?)null);
            _budgetGuardMock.Setup(b => b.TryConsumeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var spMock = new Mock<IServiceProvider>();
            spMock.Setup(s => s.GetService(typeof(ILiveViewCapableProvider))).Returns(mockProvider.Object);

            var orchestrator = CreateOrchestrator(spMock);

            // Act
            var result = await orchestrator.StartAsync(deviceId, LiveViewTriggerReason.Manual, null, CancellationToken.None);

            // Assert
            _publisherMock.Verify(p => p.PublishSessionChangedAsync(
                It.Is<LiveViewSession>(s => s.Id == result.Id && s.State == LiveViewSessionState.Starting),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task StartAsync_PublishThrows_StillReturnsSession()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var device = new Device { Id = deviceId, ProviderDeviceId = "ring-device-123", LocationId = Guid.NewGuid(), Name = "Test", Type = "camera" };
            var mockProvider = new Mock<ILiveViewCapableProvider>();
            mockProvider.Setup(p => p.StartLiveViewAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Mock<ILiveViewConnection>().Object);

            _deviceRepositoryMock.Setup(r => r.GetAsync(deviceId, It.IsAny<CancellationToken>())).ReturnsAsync(device);
            _sessionRepositoryMock.Setup(r => r.GetActiveForDeviceAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((LiveViewSession?)null);
            _budgetGuardMock.Setup(b => b.TryConsumeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _publisherMock.Setup(p => p.PublishSessionChangedAsync(It.IsAny<LiveViewSession>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("hub unavailable"));

            var spMock = new Mock<IServiceProvider>();
            spMock.Setup(s => s.GetService(typeof(ILiveViewCapableProvider))).Returns(mockProvider.Object);

            var orchestrator = CreateOrchestrator(spMock);

            // Act
            var result = await orchestrator.StartAsync(deviceId, LiveViewTriggerReason.Manual, null, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(deviceId, result.DeviceId);
        }

        [Fact]
        public async Task PromoteToSustainedAsync_ActiveSession_PublishesSessionChanged()
        {
            // Arrange
            var sessionId = Guid.NewGuid();
            _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ActiveSession(sessionId));

            var orchestrator = CreateOrchestrator();

            // Act
            await orchestrator.PromoteToSustainedAsync(sessionId, "Sustained bitrate degradation", CancellationToken.None);

            // Assert
            _publisherMock.Verify(p => p.PublishSessionChangedAsync(
                It.Is<LiveViewSession>(s => s.Id == sessionId && s.State == LiveViewSessionState.Sustained),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DemoteFromSustainedAsync_SustainedSession_PublishesSessionChanged()
        {
            // Arrange
            var sessionId = Guid.NewGuid();
            var session = ActiveSession(sessionId);
            session.State = LiveViewSessionState.Sustained;
            session.IsSustained = true;
            _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);

            var orchestrator = CreateOrchestrator();

            // Act
            await orchestrator.DemoteFromSustainedAsync(sessionId, CancellationToken.None);

            // Assert
            _publisherMock.Verify(p => p.PublishSessionChangedAsync(
                It.Is<LiveViewSession>(s => s.Id == sessionId && s.State == LiveViewSessionState.Active),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task StopAsync_ActiveSession_PublishesSessionChanged()
        {
            // Arrange
            var sessionId = Guid.NewGuid();
            _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ActiveSession(sessionId));

            var orchestrator = CreateOrchestrator();

            // Act
            await orchestrator.StopAsync(sessionId, "ManualStop", CancellationToken.None);

            // Assert
            _publisherMock.Verify(p => p.PublishSessionChangedAsync(
                It.Is<LiveViewSession>(s => s.Id == sessionId && s.State == LiveViewSessionState.Stopped),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task StopAsync_PublishThrows_StillPersistsStoppedState()
        {
            // Arrange
            var sessionId = Guid.NewGuid();
            _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ActiveSession(sessionId));
            _publisherMock.Setup(p => p.PublishSessionChangedAsync(It.IsAny<LiveViewSession>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("hub unavailable"));

            var orchestrator = CreateOrchestrator();

            // Act
            await orchestrator.StopAsync(sessionId, "ManualStop", CancellationToken.None);

            // Assert
            _sessionRepositoryMock.Verify(r => r.UpsertSessionAsync(
                It.Is<LiveViewSession>(s => s.Id == sessionId && s.State == LiveViewSessionState.Stopped),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExtendAsync_ActiveSession_DoesNotPublish()
        {
            // Arrange
            var sessionId = Guid.NewGuid();
            _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ActiveSession(sessionId));

            var orchestrator = CreateOrchestrator();

            // Act
            await orchestrator.ExtendAsync(sessionId, CancellationToken.None);

            // Assert
            _publisherMock.Verify(p => p.PublishSessionChangedAsync(It.IsAny<LiveViewSession>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
