using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Core.Tools;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Forensics;
using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Providers.Ring.Implementations;
using VideoForensics.Providers.Ring.Interfaces;

using Xunit;

using Device = VideoForensics.Data.Common.Entities.Device;

namespace VideoForensics.Client.Core.Tests
{
    /// <summary>
    /// Verifies the telemetry sampler: receiver reports from the provider connection become persisted,
    /// scored, and published <see cref="LiveViewTelemetrySample"/> rows, written in order by a single consumer.
    /// </summary>
    public class LiveViewSessionOrchestratorSamplerTests
    {
        // Saturday 2026-10-10 14:30 UTC: exercises the weekend bucket and the UTC hour.
        private static readonly DateTime SampleTimeUtc = new(2026, 10, 10, 14, 30, 0, DateTimeKind.Utc);

        private readonly Guid _deviceId = Guid.NewGuid();
        private readonly Mock<ILogger<LiveViewSessionOrchestrator>> _loggerMock = new();
        private readonly Mock<ILiveViewSessionRepository> _sessionRepositoryMock = new();
        private readonly Mock<IDeviceRepository> _deviceRepositoryMock = new();
        private readonly Mock<IProviderApiBudgetGuard> _budgetGuardMock = new();
        private readonly Mock<IForensicsConfiguration> _configMock = new();
        private readonly Mock<ILiveViewTelemetryRepository> _telemetryRepositoryMock = new();
        private readonly Mock<ICameraBitrateBaselineRepository> _baselineRepositoryMock = new();
        private readonly Mock<ILiveViewTelemetryPublisher> _publisherMock = new();
        private readonly Mock<ILiveViewCapableProvider> _providerMock = new();
        private readonly Mock<ILiveViewConnection> _connectionMock = new();
        private readonly Mock<ILiveViewInterferenceScorer> _scorerMock = new();
        private readonly List<LiveViewTelemetrySample> _written = new();

        public LiveViewSessionOrchestratorSamplerTests()
        {
            _configMock.Setup(c => c.EnableLiveView).Returns(true);

            _deviceRepositoryMock.Setup(r => r.GetAsync(_deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Device { Id = _deviceId, ProviderDeviceId = "ring-device-123", LocationId = Guid.NewGuid(), Name = "Test", Type = "camera" });
            _sessionRepositoryMock.Setup(r => r.GetActiveForDeviceAsync(_deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((LiveViewSession?)null);
            _sessionRepositoryMock.Setup(r => r.UpsertSessionAsync(It.IsAny<LiveViewSession>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((LiveViewSession s, CancellationToken _) => s);
            _sessionRepositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, CancellationToken _) => new LiveViewSession { Id = id, State = LiveViewSessionState.Active, TriggerReason = LiveViewTriggerReason.Manual });
            _budgetGuardMock.Setup(b => b.TryConsumeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _providerMock.Setup(p => p.StartLiveViewAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(_connectionMock.Object);

            SetupWritesToList();
        }

        private void SetupWritesToList()
        {
            _telemetryRepositoryMock.Setup(r => r.AddSampleAsync(It.IsAny<LiveViewTelemetrySample>(), It.IsAny<CancellationToken>()))
                .Returns((LiveViewTelemetrySample s, CancellationToken _) =>
                {
                    _written.Add(s);
                    return Task.CompletedTask;
                });
        }

        private LiveViewSessionOrchestrator CreateOrchestrator(ILiveViewInterferenceScorer? scorer = null)
        {
            var sp = new Mock<IServiceProvider>();
            sp.Setup(s => s.GetService(typeof(ILiveViewSessionRepository))).Returns(_sessionRepositoryMock.Object);
            sp.Setup(s => s.GetService(typeof(IDeviceRepository))).Returns(_deviceRepositoryMock.Object);
            sp.Setup(s => s.GetService(typeof(IProviderApiBudgetGuard))).Returns(_budgetGuardMock.Object);
            sp.Setup(s => s.GetService(typeof(ILiveViewCapableProvider))).Returns(_providerMock.Object);
            sp.Setup(s => s.GetService(typeof(ILiveViewTelemetryRepository))).Returns(_telemetryRepositoryMock.Object);
            sp.Setup(s => s.GetService(typeof(ICameraBitrateBaselineRepository))).Returns(_baselineRepositoryMock.Object);
            sp.Setup(s => s.GetService(typeof(INotificationDispatcher))).Returns(Mock.Of<INotificationDispatcher>());

            var scopeMock = new Mock<IServiceScope>();
            scopeMock.Setup(s => s.ServiceProvider).Returns(sp.Object);

            var scopeFactoryMock = new Mock<IServiceScopeFactory>();
            scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

            return new LiveViewSessionOrchestrator(
                _loggerMock.Object,
                scopeFactoryMock.Object,
                scorer ?? _scorerMock.Object,
                _configMock.Object,
                _publisherMock.Object);
        }

        private async Task<LiveViewSession> StartSessionAsync(LiveViewSessionOrchestrator orchestrator)
        {
            return await orchestrator.StartAsync(_deviceId, LiveViewTriggerReason.Manual, null, CancellationToken.None);
        }

        private void RaiseReceiverReport(byte fractionLost, int packetsLost, uint jitter, DateTime receivedAtUtc)
        {
            _connectionMock.Raise(c => c.OnReceiverReport += null,
                new RTCPReceiverReportSampleDto(fractionLost, packetsLost, jitter, receivedAtUtc));
        }

        private void RaiseBitrate(long bps)
        {
            _connectionMock.Raise(c => c.OnBitrateSampleBps += null, bps);
        }

        [Fact]
        public async Task LiveViewSessionOrchestrator_ReceiverReport_WritesOneSample()
        {
            // Arrange
            _scorerMock.Setup(s => s.ScoreSample(It.IsAny<LiveViewTelemetrySample>(), null, null)).Returns(0.25);
            var orchestrator = CreateOrchestrator();
            var session = await StartSessionAsync(orchestrator);

            // Act: 128/256 is exactly 50 percent
            RaiseReceiverReport(fractionLost: 128, packetsLost: 7, jitter: 900, receivedAtUtc: SampleTimeUtc);
            await orchestrator.StopAsync(session.Id, "test", CancellationToken.None);

            // Assert
            _telemetryRepositoryMock.Verify(r => r.AddSampleAsync(It.IsAny<LiveViewTelemetrySample>(), It.IsAny<CancellationToken>()), Times.Once);
            var sample = Assert.Single(_written);
            Assert.Equal(session.Id, sample.SessionId);
            Assert.Equal(SampleTimeUtc, sample.CapturedAtUtc);
            Assert.Equal((byte)50, sample.FractionLost);
            Assert.Equal(7, sample.CumulativePacketsLost);
            Assert.Equal(900u, sample.JitterTicks);
            Assert.Null(sample.BitrateBps);
            Assert.Equal(0.25, sample.InterferenceScore);
        }

        [Fact]
        public async Task LiveViewSessionOrchestrator_BitrateEvent_AloneWritesNothing()
        {
            // Arrange
            var orchestrator = CreateOrchestrator();
            var session = await StartSessionAsync(orchestrator);

            // Act
            RaiseBitrate(2_500_000L);
            await orchestrator.StopAsync(session.Id, "test", CancellationToken.None);

            // Assert
            Assert.Empty(_written);
            _telemetryRepositoryMock.Verify(r => r.AddSampleAsync(It.IsAny<LiveViewTelemetrySample>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task LiveViewSessionOrchestrator_BitrateEvent_MergedIntoNextReceiverReportSample()
        {
            // Arrange
            var orchestrator = CreateOrchestrator();
            var session = await StartSessionAsync(orchestrator);

            // Act
            RaiseBitrate(2_500_000L);
            RaiseReceiverReport(fractionLost: 0, packetsLost: 0, jitter: 10, receivedAtUtc: SampleTimeUtc);
            await orchestrator.StopAsync(session.Id, "test", CancellationToken.None);

            // Assert
            var sample = Assert.Single(_written);
            Assert.Equal(2_500_000L, sample.BitrateBps);
        }

        [Fact]
        public async Task LiveViewSessionOrchestrator_NoBaseline_InterferenceScoreIsNull()
        {
            // Arrange: real scorer, and no bucket or device-global baseline exists for the device
            _baselineRepositoryMock.Setup(r => r.GetBucketAsync(_deviceId, 14, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync((CameraBitrateBaseline?)null);
            _baselineRepositoryMock.Setup(r => r.GetDeviceGlobalAsync(_deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((CameraBitrateBaseline?)null);
            var orchestrator = CreateOrchestrator(new LiveViewInterferenceScorer());
            var session = await StartSessionAsync(orchestrator);

            // Act
            RaiseReceiverReport(fractionLost: 128, packetsLost: 7, jitter: 900, receivedAtUtc: SampleTimeUtc);
            await orchestrator.StopAsync(session.Id, "test", CancellationToken.None);

            // Assert: the weekend bucket for the UTC hour was queried, and an unscorable sample stays null (not zero)
            _baselineRepositoryMock.Verify(r => r.GetBucketAsync(_deviceId, 14, true, It.IsAny<CancellationToken>()), Times.Once);
            var sample = Assert.Single(_written);
            Assert.Null(sample.InterferenceScore);
        }

        [Fact]
        public async Task LiveViewSessionOrchestrator_ScorerThrows_StillPersistsAndKeepsSessionActive()
        {
            // Arrange
            _scorerMock.Setup(s => s.ScoreSample(It.IsAny<LiveViewTelemetrySample>(), It.IsAny<CameraBitrateBaseline?>(), It.IsAny<CameraBitrateBaseline?>()))
                .Throws(new InvalidOperationException("scorer failure"));
            var orchestrator = CreateOrchestrator();
            var session = await StartSessionAsync(orchestrator);

            // Act
            RaiseReceiverReport(fractionLost: 128, packetsLost: 7, jitter: 900, receivedAtUtc: SampleTimeUtc);
            await orchestrator.StopAsync(session.Id, "test", CancellationToken.None);

            // Assert: the row is persisted without a score, and the session was never moved to a failed state
            var sample = Assert.Single(_written);
            Assert.Null(sample.InterferenceScore);
            _sessionRepositoryMock.Verify(r => r.UpsertSessionAsync(
                It.Is<LiveViewSession>(s => s.State == LiveViewSessionState.Failed), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task LiveViewSessionOrchestrator_PublishThrows_StillPersists()
        {
            // Arrange
            _publisherMock.Setup(p => p.PublishSampleAsync(It.IsAny<LiveViewTelemetrySample>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("transport down"));
            var orchestrator = CreateOrchestrator();
            var session = await StartSessionAsync(orchestrator);

            // Act
            RaiseReceiverReport(fractionLost: 128, packetsLost: 7, jitter: 900, receivedAtUtc: SampleTimeUtc);
            await orchestrator.StopAsync(session.Id, "test", CancellationToken.None);

            // Assert
            _telemetryRepositoryMock.Verify(r => r.AddSampleAsync(It.IsAny<LiveViewTelemetrySample>(), It.IsAny<CancellationToken>()), Times.Once);
            Assert.Single(_written);
        }

        [Fact]
        public async Task LiveViewSessionOrchestrator_Persist_RepositoryThrows_LogsAndContinuesWithNextSample()
        {
            // Arrange: the first write fails, the second succeeds
            int attempts = 0;
            _telemetryRepositoryMock.Setup(r => r.AddSampleAsync(It.IsAny<LiveViewTelemetrySample>(), It.IsAny<CancellationToken>()))
                .Returns((LiveViewTelemetrySample s, CancellationToken _) =>
                {
                    attempts++;
                    if (attempts == 1)
                    {
                        throw new InvalidOperationException("database unavailable");
                    }

                    _written.Add(s);
                    return Task.CompletedTask;
                });
            var orchestrator = CreateOrchestrator();
            var session = await StartSessionAsync(orchestrator);

            // Act
            RaiseReceiverReport(fractionLost: 1, packetsLost: 1, jitter: 1, receivedAtUtc: SampleTimeUtc);
            RaiseReceiverReport(fractionLost: 2, packetsLost: 2, jitter: 2, receivedAtUtc: SampleTimeUtc.AddSeconds(2));
            await orchestrator.StopAsync(session.Id, "test", CancellationToken.None);

            // Assert
            _telemetryRepositoryMock.Verify(r => r.AddSampleAsync(It.IsAny<LiveViewTelemetrySample>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            Assert.Single(_written);
            Assert.Equal(2, _written[0].CumulativePacketsLost);
            _loggerMock.Verify(l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task LiveViewSessionOrchestrator_Stop_UnsubscribesAndNoWriteAfterStop()
        {
            // Arrange
            var orchestrator = CreateOrchestrator();
            var session = await StartSessionAsync(orchestrator);
            RaiseReceiverReport(fractionLost: 1, packetsLost: 1, jitter: 1, receivedAtUtc: SampleTimeUtc);
            await orchestrator.StopAsync(session.Id, "test", CancellationToken.None);

            // Act: events raised after stop must not reach the sampler
            RaiseReceiverReport(fractionLost: 9, packetsLost: 9, jitter: 9, receivedAtUtc: SampleTimeUtc.AddSeconds(5));
            RaiseBitrate(1_000L);

            // Assert
            _telemetryRepositoryMock.Verify(r => r.AddSampleAsync(It.IsAny<LiveViewTelemetrySample>(), It.IsAny<CancellationToken>()), Times.Once);
            Assert.Single(_written);
        }

        [Fact]
        public async Task LiveViewSessionOrchestrator_Stop_DrainsQueuedSamplesBeforeClose()
        {
            // Arrange: record how many rows were written at the moment the connection is closed
            int writesAtClose = -1;
            _connectionMock.Setup(c => c.CloseAsync(It.IsAny<CancellationToken>()))
                .Callback<CancellationToken>(_ => writesAtClose = _written.Count)
                .Returns(Task.CompletedTask);
            var orchestrator = CreateOrchestrator();
            var session = await StartSessionAsync(orchestrator);

            // Act: three reports are queued, then stop is requested
            RaiseReceiverReport(fractionLost: 1, packetsLost: 1, jitter: 1, receivedAtUtc: SampleTimeUtc);
            RaiseReceiverReport(fractionLost: 2, packetsLost: 2, jitter: 2, receivedAtUtc: SampleTimeUtc.AddSeconds(2));
            RaiseReceiverReport(fractionLost: 3, packetsLost: 3, jitter: 3, receivedAtUtc: SampleTimeUtc.AddSeconds(4));
            await orchestrator.StopAsync(session.Id, "test", CancellationToken.None);

            // Assert: all queued rows were written before the close, so the drain is deterministic
            Assert.Equal(3, writesAtClose);
            Assert.Equal(3, _written.Count);
        }

        [Fact]
        public async Task LiveViewSessionOrchestrator_SamplesWrittenInOrder()
        {
            // Arrange
            var orchestrator = CreateOrchestrator();
            var session = await StartSessionAsync(orchestrator);

            // Act
            RaiseReceiverReport(fractionLost: 1, packetsLost: 1, jitter: 1, receivedAtUtc: SampleTimeUtc);
            RaiseReceiverReport(fractionLost: 1, packetsLost: 2, jitter: 1, receivedAtUtc: SampleTimeUtc.AddSeconds(2));
            await orchestrator.StopAsync(session.Id, "test", CancellationToken.None);

            // Assert
            Assert.Equal(new int?[] { 1, 2 }, _written.Select(s => s.CumulativePacketsLost).ToArray());
        }
    }
}
