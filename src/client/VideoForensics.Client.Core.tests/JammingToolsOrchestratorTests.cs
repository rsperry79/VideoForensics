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
        private readonly Mock<IDeviceHealthRepository> _healthRepositoryMock;
        private readonly JammingToolsOrchestrator _orchestrator;

        public JammingToolsOrchestratorTests()
        {
            _loggerMock = new Mock<ILogger<JammingToolsOrchestrator>>();
            _repositoryMock = new Mock<IJammingRepository>();
            _healthRepositoryMock = new Mock<IDeviceHealthRepository>();
            _orchestrator = new JammingToolsOrchestrator(
                _loggerMock.Object,
                _repositoryMock.Object,
                _healthRepositoryMock.Object);

            // Remote clients throw NotSupportedException for the unranged history; analysis must never call it.
            _ = _healthRepositoryMock
                .Setup(r => r.GetHistoryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new NotSupportedException("Unranged history is not supported remotely"));
        }

        [Fact]
        public async Task RecordJammingIncidentAsync_RejectsInvalidTimeRange()
        {
            var deviceId = Guid.NewGuid();
            DateTime now = DateTime.UtcNow;

            (bool Success, string Message, JammingIncidentRecord? Record) result = await _orchestrator.RecordJammingIncidentAsync(
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
            DateTime now = DateTime.UtcNow;

            (bool Success, string Message, JammingIncidentRecord? Record) result = await _orchestrator.RecordJammingIncidentAsync(
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
            DateTime now = DateTime.UtcNow;

            JammingIncidentRecord? capturedRecord = null;
            _ = _repositoryMock
                .Setup(r => r.UpsertIncidentAsync(It.IsAny<JammingIncidentRecord>(), It.IsAny<CancellationToken>()))
                .Callback<JammingIncidentRecord, CancellationToken>((record, ct) => capturedRecord = record)
                .ReturnsAsync((JammingIncidentRecord record, CancellationToken ct) => record);

            (bool Success, string Message, JammingIncidentRecord? Record) result = await _orchestrator.RecordJammingIncidentAsync(
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

            _ = _repositoryMock
                .Setup(r => r.GetStatsAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((JammingStatsSummary?)null);

            (bool Success, JammingStatsSummary? Stats) = await _orchestrator.GetJammingStatsAsync(deviceId);

            Assert.True(Success);
            Assert.NotNull(Stats);
            Assert.Equal(deviceId, Stats.DeviceId);
            Assert.Equal(0, Stats.IncidentCount);
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

            _ = _repositoryMock
                .Setup(r => r.GetStatsAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(stats);

            (bool Success, JammingStatsSummary? Stats) = await _orchestrator.GetJammingStatsAsync(deviceId);

            Assert.True(Success);
            Assert.NotNull(Stats);
            Assert.Equal(3, Stats.IncidentCount);
            Assert.Equal(45.0, Stats.TotalJammedDurationMinutes);
        }

        [Fact]
        public async Task GetJammingIncidentsAsync_ReturnsFilteredIncidents()
        {
            var deviceId = Guid.NewGuid();
            DateTime now = DateTime.UtcNow;
            var incidents = (IReadOnlyList<JammingIncidentRecord>)
            [
                new JammingIncidentRecord { Id = Guid.NewGuid(), DeviceId = deviceId, StartUtc = now }
            ];

            _ = _repositoryMock
                .Setup(r => r.ListIncidentsAsync(deviceId, now, now.AddHours(1), It.IsAny<CancellationToken>()))
                .ReturnsAsync(incidents);

            (bool Success, IReadOnlyList<JammingIncidentRecord>? Incidents) = await _orchestrator.GetJammingIncidentsAsync(deviceId, now, now.AddHours(1));

            Assert.True(Success);
            Assert.NotNull(Incidents);
            _ = Assert.Single(Incidents);
        }

        [Fact]
        public async Task AnalyzeJammingAsync_RejectsInvalidTimeRange()
        {
            var deviceId = Guid.NewGuid();
            DateTime now = DateTime.UtcNow;

            JammingAnalysisReport report = await _orchestrator.AnalyzeJammingAsync(deviceId, now.AddHours(1), now);

            Assert.False(report.Success);
            Assert.Contains("Start time must be before end time", report.ErrorMessage);
        }

        [Fact]
        public async Task AnalyzeJammingAsync_TooFewReadings_DetectsNoIncidents()
        {
            var deviceId = Guid.NewGuid();
            DateTime now = DateTime.UtcNow;

            _ = _healthRepositoryMock
                .Setup(r => r.GetHistoryAsync(deviceId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                [
                    new() { DeviceId = deviceId, WifiSignalRssi = -40, CapturedAtUtc = now }
                ]);
            _ = _repositoryMock
                .Setup(r => r.ListIncidentsAsync(deviceId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
            _ = _repositoryMock
                .Setup(r => r.GetStatsAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new JammingStatsSummary { DeviceId = deviceId, IncidentCount = 0 });

            JammingAnalysisReport report = await _orchestrator.AnalyzeJammingAsync(deviceId, now.AddMinutes(-10), now.AddMinutes(10));

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
            DateTime t0 = DateTime.UtcNow;

            // Established baseline around -40 dBm (8 readings), then a sustained drop to ~-60 dBm
            // (20 dB degradation) across 3 consecutive readings, then recovery back to baseline.
            // Degraded readings are a minority of the sample, as in realistic conditions, so the
            // median baseline isn't skewed by the incident itself.
            var readings = new List<DeviceHealth>();
            for (int i = 0; i < 8; i++)
            {
                readings.Add(new DeviceHealth { DeviceId = deviceId, WifiSignalRssi = -40 - (i % 3), CapturedAtUtc = t0.AddMinutes(i) });
            }

            readings.Add(new DeviceHealth { DeviceId = deviceId, WifiSignalRssi = -60, CapturedAtUtc = t0.AddMinutes(8) });
            readings.Add(new DeviceHealth { DeviceId = deviceId, WifiSignalRssi = -62, CapturedAtUtc = t0.AddMinutes(9) });
            readings.Add(new DeviceHealth { DeviceId = deviceId, WifiSignalRssi = -59, CapturedAtUtc = t0.AddMinutes(10) });
            readings.Add(new DeviceHealth { DeviceId = deviceId, WifiSignalRssi = -39, CapturedAtUtc = t0.AddMinutes(11) });

            _ = _healthRepositoryMock
                .Setup(r => r.GetHistoryAsync(deviceId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(readings);
            _ = _repositoryMock
                .Setup(r => r.ListIncidentsAsync(deviceId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
            _ = _repositoryMock
                .Setup(r => r.GetStatsAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new JammingStatsSummary { DeviceId = deviceId, IncidentCount = 1, HighConfidenceCount = 1 });

            JammingIncidentRecord? captured = null;
            _ = _repositoryMock
                .Setup(r => r.UpsertIncidentAsync(It.IsAny<JammingIncidentRecord>(), It.IsAny<CancellationToken>()))
                .Callback<JammingIncidentRecord, CancellationToken>((record, ct) => captured = record)
                .ReturnsAsync((JammingIncidentRecord record, CancellationToken ct) => record);

            JammingAnalysisReport report = await _orchestrator.AnalyzeJammingAsync(deviceId, t0.AddMinutes(-1), t0.AddMinutes(10));

            Assert.True(report.Success);
            Assert.NotNull(captured);
            Assert.Equal(JammingIncidentSource.AutoDetected, captured!.Source);
            Assert.Equal(3, captured.AffectedEventCount);
            Assert.True(captured.AverageDegradationDb >= 15);
            _repositoryMock.Verify(r => r.RecomputeStatsAsync(deviceId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task AnalyzeJammingAsync_NewIncidentsDetected_SetsNewlyDetectedCount()
        {
            var deviceId = Guid.NewGuid();
            DateTime t0 = DateTime.UtcNow;

            // Create readings with a sustained drop to trigger incident detection
            var readings = new List<DeviceHealth>();
            for (int i = 0; i < 8; i++)
            {
                readings.Add(new DeviceHealth { DeviceId = deviceId, WifiSignalRssi = -40, CapturedAtUtc = t0.AddMinutes(i) });
            }

            // Degraded section: 3 readings with ~20 dB drop
            readings.Add(new DeviceHealth { DeviceId = deviceId, WifiSignalRssi = -60, CapturedAtUtc = t0.AddMinutes(8) });
            readings.Add(new DeviceHealth { DeviceId = deviceId, WifiSignalRssi = -62, CapturedAtUtc = t0.AddMinutes(9) });
            readings.Add(new DeviceHealth { DeviceId = deviceId, WifiSignalRssi = -59, CapturedAtUtc = t0.AddMinutes(10) });
            readings.Add(new DeviceHealth { DeviceId = deviceId, WifiSignalRssi = -40, CapturedAtUtc = t0.AddMinutes(11) });

            _ = _healthRepositoryMock
                .Setup(r => r.GetHistoryAsync(deviceId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(readings);
            _ = _repositoryMock
                .Setup(r => r.ListIncidentsAsync(deviceId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
            _ = _repositoryMock
                .Setup(r => r.GetStatsAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new JammingStatsSummary { DeviceId = deviceId, IncidentCount = 1, HighConfidenceCount = 1 });

            _ = _repositoryMock
                .Setup(r => r.UpsertIncidentAsync(It.IsAny<JammingIncidentRecord>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((JammingIncidentRecord record, CancellationToken ct) => record);

            JammingAnalysisReport report = await _orchestrator.AnalyzeJammingAsync(deviceId, t0.AddMinutes(-1), t0.AddMinutes(11));

            Assert.True(report.Success);
            Assert.Equal(1, report.NewlyDetectedCount);
        }

        [Fact]
        public async Task AnalyzeJammingAsync_RequestsRangedHistoryForTheAnalysisWindow_NeverTheUnrangedOverload()
        {
            var deviceId = Guid.NewGuid();
            DateTime from = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime to = new(2026, 3, 1, 6, 0, 0, DateTimeKind.Utc);
            _ = _healthRepositoryMock
                .Setup(r => r.GetHistoryAsync(deviceId, from, to, It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
            _ = _repositoryMock
                .Setup(r => r.GetStatsAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((JammingStatsSummary?)null);
            _ = _repositoryMock
                .Setup(r => r.ListIncidentsAsync(deviceId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            JammingAnalysisReport report = await _orchestrator.AnalyzeJammingAsync(deviceId, from, to);

            Assert.True(report.Success);
            _healthRepositoryMock.Verify(r => r.GetHistoryAsync(deviceId, from, to, It.IsAny<CancellationToken>()), Times.Once);
            _healthRepositoryMock.Verify(r => r.GetHistoryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        /// <summary>
        /// Mimics the remote jamming API: the client's Source is preserved for new incidents, the server
        /// assigns the CaseId and DetectedAtUtc, and ListIncidentsAsync filters on the incident start.
        /// </summary>
        private sealed class RemoteSemanticsJammingRepository : IJammingRepository
        {
            public List<JammingIncidentRecord> Stored { get; } = [];
            public int UpsertCalls { get; private set; }

            public Task<JammingIncidentRecord> UpsertIncidentAsync(JammingIncidentRecord incident, CancellationToken ct)
            {
                UpsertCalls++;
                JammingIncidentRecord? existing = Stored.FirstOrDefault(i => i.Id == incident.Id);
                if (existing != null)
                {
                    Stored.Remove(existing);
                }

                var saved = new JammingIncidentRecord
                {
                    Id = incident.Id,
                    DeviceId = incident.DeviceId,
                    StartUtc = incident.StartUtc,
                    EndUtc = incident.EndUtc,
                    AffectedEventCount = incident.AffectedEventCount,
                    AverageDegradationDb = incident.AverageDegradationDb,
                    Confidence = incident.Confidence,
                    Notes = incident.Notes,
                    Source = existing?.Source ?? incident.Source,
                    DetectedAtUtc = existing?.DetectedAtUtc ?? DateTime.UtcNow,
                    CaseId = existing?.CaseId ?? Guid.NewGuid()
                };
                Stored.Add(saved);
                return Task.FromResult(saved);
            }

            public Task<IReadOnlyList<JammingIncidentRecord>> ListIncidentsAsync(Guid? deviceId, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct)
            {
                IReadOnlyList<JammingIncidentRecord> result = Stored
                    .Where(i => (deviceId == null || i.DeviceId == deviceId)
                        && (fromUtc == null || i.StartUtc >= fromUtc)
                        && (toUtc == null || i.StartUtc <= toUtc))
                    .ToList();
                return Task.FromResult(result);
            }

            public Task<JammingIncidentRecord?> GetIncidentAsync(Guid incidentId, CancellationToken ct)
                => Task.FromResult(Stored.FirstOrDefault(i => i.Id == incidentId));

            public Task<JammingStatsSummary?> GetStatsAsync(Guid deviceId, CancellationToken ct)
                => Task.FromResult<JammingStatsSummary?>(new JammingStatsSummary
                {
                    DeviceId = deviceId,
                    IncidentCount = Stored.Count(i => i.DeviceId == deviceId)
                });

            public Task<IReadOnlyList<JammingStatsSummary>> ListStatsAsync(CancellationToken ct)
                => Task.FromResult<IReadOnlyList<JammingStatsSummary>>([]);

            public Task<JammingStatsSummary> RecomputeStatsAsync(Guid deviceId, CancellationToken ct)
                => Task.FromResult(new JammingStatsSummary { DeviceId = deviceId, IncidentCount = Stored.Count(i => i.DeviceId == deviceId) });
        }

        [Fact]
        public async Task AnalyzeJammingAsync_RemoteSemantics_CreatesAutoDetectedIncidentsOnce_AndSecondRunCreatesNone()
        {
            var deviceId = Guid.NewGuid();
            DateTime t0 = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

            // 10 baseline, 3 degraded, 6 baseline, 3 degraded, 4 baseline (two separate jamming runs).
            int[] rssi = [.. Enumerable.Repeat(-40, 10), -60, -62, -59, .. Enumerable.Repeat(-40, 6), -61, -60, -63, .. Enumerable.Repeat(-40, 4)];
            List<DeviceHealth> readings = rssi
                .Select((v, i) => new DeviceHealth { DeviceId = deviceId, WifiSignalRssi = v, CapturedAtUtc = t0.AddMinutes(i) })
                .ToList();
            DateTime from = t0.AddMinutes(-1);
            DateTime to = t0.AddMinutes(rssi.Length);

            _ = _healthRepositoryMock
                .Setup(r => r.GetHistoryAsync(deviceId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid _, DateTime f, DateTime t, CancellationToken _) =>
                    (IReadOnlyList<DeviceHealth>)readings.Where(r => r.CapturedAtUtc >= f && r.CapturedAtUtc <= t).OrderBy(r => r.CapturedAtUtc).ToList());
            var remote = new RemoteSemanticsJammingRepository();
            var orchestrator = new JammingToolsOrchestrator(_loggerMock.Object, remote, _healthRepositoryMock.Object);

            JammingAnalysisReport first = await orchestrator.AnalyzeJammingAsync(deviceId, from, to);
            JammingAnalysisReport second = await orchestrator.AnalyzeJammingAsync(deviceId, from, to);

            Assert.True(first.Success);
            Assert.Equal(2, first.NewlyDetectedCount);
            Assert.Equal(2, remote.Stored.Count);
            Assert.All(remote.Stored, i => Assert.Equal(JammingIncidentSource.AutoDetected, i.Source));
            Assert.All(remote.Stored, i => Assert.NotNull(i.CaseId));
            Assert.True(second.Success);
            Assert.Equal(0, second.NewlyDetectedCount);
            Assert.Equal(2, remote.Stored.Count);
            Assert.Equal(2, remote.UpsertCalls);
            _healthRepositoryMock.Verify(r => r.GetHistoryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
