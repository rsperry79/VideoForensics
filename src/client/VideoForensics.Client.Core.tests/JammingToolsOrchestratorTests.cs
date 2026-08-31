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
        private readonly Mock<IDeviceHealthSnapshotRepository> _healthSnapshotRepositoryMock;
        private readonly JammingToolsOrchestrator _orchestrator;

        public JammingToolsOrchestratorTests()
        {
            _loggerMock = new Mock<ILogger<JammingToolsOrchestrator>>();
            _repositoryMock = new Mock<IJammingRepository>();
            _healthSnapshotRepositoryMock = new Mock<IDeviceHealthSnapshotRepository>();
            _orchestrator = new JammingToolsOrchestrator(
                _loggerMock.Object,
                _repositoryMock.Object,
                _healthSnapshotRepositoryMock.Object);
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
                JammingConfidenceLevel.Medium,
                notes: null);

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
                JammingConfidenceLevel.Medium,
                notes: null);

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

        [Fact]
        public async Task AnalyzeJammingAsync_RejectsInvalidTimeRange()
        {
            var deviceId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            var report = await _orchestrator.AnalyzeJammingAsync(deviceId, now.AddHours(1), now);

            Assert.False(report.Success);
            Assert.Contains("Start time must be before end time", report.ErrorMessage);
        }

        [Fact]
        public async Task AnalyzeJammingAsync_TooFewReadings_DetectsNoIncidents()
        {
            var deviceId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            _healthSnapshotRepositoryMock
                .Setup(r => r.GetHistoryAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<DeviceHealthSnapshot>
                {
                    new DeviceHealthSnapshot { DeviceId = deviceId, Rssi = -40, CapturedAtUtc = now }
                });
            _repositoryMock
                .Setup(r => r.ListIncidentsAsync(deviceId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<JammingIncidentRecord>());
            _repositoryMock
                .Setup(r => r.GetStatsAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new JammingStatsSummary { DeviceId = deviceId, IncidentCount = 0 });

            var report = await _orchestrator.AnalyzeJammingAsync(deviceId, now.AddMinutes(-10), now.AddMinutes(10));

            Assert.True(report.Success);
            Assert.Equal(0, report.Summary.IncidentCount);
            _repositoryMock.Verify(
                r => r.UpsertIncidentAsync(It.IsAny<JammingIncidentRecord>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task AnalyzeJammingAsync_SustainedDrop_DetectsAndPersistsIncident()
        {
            var deviceId = Guid.NewGuid();
            var t0 = DateTime.UtcNow;

            // Established baseline around -40 dBm (8 readings), then a sustained drop to ~-60 dBm
            // (20 dB degradation) across 3 consecutive readings, then recovery back to baseline.
            // Degraded readings are a minority of the sample, as in realistic conditions, so the
            // median baseline isn't skewed by the incident itself.
            var readings = new List<DeviceHealthSnapshot>();
            for (var i = 0; i < 8; i++)
            {
                readings.Add(new DeviceHealthSnapshot { DeviceId = deviceId, Rssi = -40 - (i % 3), CapturedAtUtc = t0.AddMinutes(i) });
            }
            readings.Add(new DeviceHealthSnapshot { DeviceId = deviceId, Rssi = -60, CapturedAtUtc = t0.AddMinutes(8) });
            readings.Add(new DeviceHealthSnapshot { DeviceId = deviceId, Rssi = -62, CapturedAtUtc = t0.AddMinutes(9) });
            readings.Add(new DeviceHealthSnapshot { DeviceId = deviceId, Rssi = -59, CapturedAtUtc = t0.AddMinutes(10) });
            readings.Add(new DeviceHealthSnapshot { DeviceId = deviceId, Rssi = -39, CapturedAtUtc = t0.AddMinutes(11) });

            _healthSnapshotRepositoryMock
                .Setup(r => r.GetHistoryAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(readings);
            _repositoryMock
                .Setup(r => r.ListIncidentsAsync(deviceId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<JammingIncidentRecord>());
            _repositoryMock
                .Setup(r => r.GetStatsAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new JammingStatsSummary { DeviceId = deviceId, IncidentCount = 1, HighConfidenceCount = 1 });

            JammingIncidentRecord? captured = null;
            _repositoryMock
                .Setup(r => r.UpsertIncidentAsync(It.IsAny<JammingIncidentRecord>(), It.IsAny<CancellationToken>()))
                .Callback<JammingIncidentRecord, CancellationToken>((record, ct) => captured = record)
                .ReturnsAsync((JammingIncidentRecord record, CancellationToken ct) => record);

            var report = await _orchestrator.AnalyzeJammingAsync(deviceId, t0.AddMinutes(-1), t0.AddMinutes(10));

            Assert.True(report.Success);
            Assert.NotNull(captured);
            Assert.Equal(JammingIncidentSource.AutoDetected, captured!.Source);
            Assert.Equal(3, captured.AffectedEventCount);
            Assert.True(captured.AverageDegradationDb >= 15);
            _repositoryMock.Verify(r => r.RecomputeStatsAsync(deviceId, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
