using Microsoft.Extensions.Logging;
using Moq;
using VideoForensics.Client.Core.Tools;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using Xunit;

namespace VideoForensics.Client.Core.Tests
{
    public class JammingToolsOrchestratorTests
    {
        private readonly Mock<ILogger<JammingToolsOrchestrator>> _loggerMock;
        private readonly Mock<IJammingRepository> _repositoryMock;
        private readonly JammingToolsOrchestrator _orchestrator;

        public JammingToolsOrchestratorTests()
        {
            _loggerMock = new Mock<ILogger<JammingToolsOrchestrator>>();
            _detectorMock = new Mock<ISignalAnomalyDetector>();
            _repositoryMock = new Mock<IJammingRepository>();
            _eventRepositoryMock = new Mock<IEventRepository>();
            _orchestrator = new JammingToolsOrchestrator(
                _loggerMock.Object,
                _repositoryMock.Object);
        }

        [Fact]
        public async Task RecordJammingIncidentAsync_RejectsInvalidTimeRange()
        {
            var deviceId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            var result = await _orchestrator.RecordJammingIncidentAsync(
                deviceId,
                now.AddHours(1),
                now, // End before start
                5,
                10.0,
                JammingConfidenceLevel.Medium);

            Assert.False(result.Success);
            Assert.Contains("Start time must be before end time", result.Message);
        }

        [Fact]
        public async Task RecordJammingIncidentAsync_RejectsNegativeDegradation()
        {
            var deviceId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            var result = await _orchestrator.RecordJammingIncidentAsync(
                deviceId,
                now,
                now.AddHours(1),
                5,
                -10.0, // Negative
                JammingConfidenceLevel.Medium);

            Assert.False(result.Success);
            Assert.Contains("must be non-negative", result.Message);
        }

        [Fact]
        public async Task RecordJammingIncidentAsync_SuccessfullyRecordsIncident()
        {
            var deviceId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            JammingIncidentRecord? capturedRecord = null;
            _repositoryMock
                .Setup(r => r.UpsertIncidentAsync(It.IsAny<JammingIncidentRecord>(), It.IsAny<CancellationToken>()))
                .Callback<JammingIncidentRecord, CancellationToken>((record, ct) => capturedRecord = record)
                .ReturnsAsync((JammingIncidentRecord record, CancellationToken ct) => record);

            var result = await _orchestrator.RecordJammingIncidentAsync(
                deviceId,
                now,
                now.AddHours(1),
                5,
                10.0,
                JammingConfidenceLevel.High,
                "Test incident",
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.NotNull(result.Record);
            Assert.Equal(JammingIncidentSource.ManuallyRecorded, capturedRecord?.Source);
            Assert.Equal("Test incident", capturedRecord?.Notes);

            _repositoryMock.Verify(
                r => r.UpsertIncidentAsync(It.IsAny<JammingIncidentRecord>(), It.IsAny<CancellationToken>()),
                Times.Once);

            _repositoryMock.Verify(
                r => r.RecomputeStatsAsync(deviceId, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GetJammingStatsAsync_ReturnsEmptyStatsWhenNoneExist()
        {
            var deviceId = Guid.NewGuid();

            _repositoryMock
                .Setup(r => r.GetStatsAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((JammingStatsSummary?)null);

            var result = await _orchestrator.GetJammingStatsAsync(deviceId);

            Assert.True(result.Success);
            Assert.NotNull(result.Stats);
            Assert.Equal(deviceId, result.Stats.DeviceId);
            Assert.Equal(0, result.Stats.IncidentCount);
        }

        [Fact]
        public async Task GetJammingStatsAsync_ReturnsExistingStats()
        {
            var deviceId = Guid.NewGuid();
            var stats = new JammingStatsSummary
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                IncidentCount = 3,
                TotalJammedDurationMinutes = 45.0,
                AverageDegradationDb = 12.5,
                MaxDegradationDb = 15.0
            };

            _repositoryMock
                .Setup(r => r.GetStatsAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(stats);

            var result = await _orchestrator.GetJammingStatsAsync(deviceId);

            Assert.True(result.Success);
            Assert.NotNull(result.Stats);
            Assert.Equal(3, result.Stats.IncidentCount);
            Assert.Equal(45.0, result.Stats.TotalJammedDurationMinutes);
        }

        [Fact]
        public async Task GetJammingIncidentsAsync_ReturnsFilteredIncidents()
        {
            var deviceId = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var incidents = (IReadOnlyList<JammingIncidentRecord>)new List<JammingIncidentRecord>
            {
                new JammingIncidentRecord { Id = Guid.NewGuid(), DeviceId = deviceId, StartUtc = now }
            };

            _repositoryMock
                .Setup(r => r.ListIncidentsAsync(deviceId, now, now.AddHours(1), It.IsAny<CancellationToken>()))
                .ReturnsAsync(incidents);

            var result = await _orchestrator.GetJammingIncidentsAsync(deviceId, now, now.AddHours(1));

            Assert.True(result.Success);
            Assert.NotNull(result.Incidents);
            Assert.Single(result.Incidents);
        }
    }
}
