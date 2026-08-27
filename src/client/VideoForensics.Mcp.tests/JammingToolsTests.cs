using Moq;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Mcp.Tools;
using VideoForensics.Providers.Ring;
using VideoForensics.Providers.Ring.Services;
using Xunit;

namespace VideoForensics.Mcp.Tests
{
    public class JammingToolsTests
    {
        private static Device MakeDevice(Guid id, string providerDeviceId = "12345") => new Device
        {
            Id = id,
            LocationId = Guid.NewGuid(),
            ProviderDeviceId = providerDeviceId,
            Name = "Front Door",
            Type = "camera",
            IsOnline = true
        };

        [Fact]
        public async Task RecordJammingIncident_PersistsIncidentAndRecomputesStats()
        {
            var deviceId = Guid.NewGuid();
            var mockJammingRepository = new Mock<IJammingRepository>();
            var mockDeviceRepository = new Mock<IDeviceRepository>();

            mockDeviceRepository
                .Setup(r => r.GetAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(MakeDevice(deviceId));

            JammingIncidentRecord? persisted = null;
            mockJammingRepository
                .Setup(r => r.UpsertIncidentAsync(It.IsAny<JammingIncidentRecord>(), It.IsAny<CancellationToken>()))
                .Callback<JammingIncidentRecord, CancellationToken>((r, _) => persisted = r)
                .ReturnsAsync((JammingIncidentRecord r, CancellationToken _) => r);

            var expectedSummary = new JammingStatsSummary { Id = Guid.NewGuid(), DeviceId = deviceId, IncidentCount = 1 };
            mockJammingRepository
                .Setup(r => r.RecomputeStatsAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedSummary);

            var start = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
            var end = start.AddMinutes(15);

            var result = await JammingTools.RecordJammingIncident(
                mockJammingRepository.Object,
                mockDeviceRepository.Object,
                deviceId,
                start,
                end,
                affectedEventCount: 4,
                averageDegradationDb: 12.5,
                confidence: JammingConfidenceLevel.Medium,
                cancellationToken: CancellationToken.None,
                notes: "manual test entry");

            Assert.Same(expectedSummary, result);
            Assert.NotNull(persisted);
            Assert.Equal(deviceId, persisted!.DeviceId);
            Assert.Equal(start, persisted.StartUtc);
            Assert.Equal(end, persisted.EndUtc);
            Assert.Equal(4, persisted.AffectedEventCount);
            Assert.Equal(12.5, persisted.AverageDegradationDb);
            Assert.Equal(JammingConfidenceLevel.Medium, persisted.Confidence);
            Assert.Equal(JammingIncidentSource.ManuallyRecorded, persisted.Source);
            Assert.Equal("manual test entry", persisted.Notes);

            mockJammingRepository.Verify(r => r.UpsertIncidentAsync(It.IsAny<JammingIncidentRecord>(), It.IsAny<CancellationToken>()), Times.Once);
            mockJammingRepository.Verify(r => r.RecomputeStatsAsync(deviceId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RecordJammingIncident_RejectsEndBeforeStart()
        {
            var deviceId = Guid.NewGuid();
            var mockJammingRepository = new Mock<IJammingRepository>();
            var mockDeviceRepository = new Mock<IDeviceRepository>();

            var start = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
            var end = start.AddMinutes(-5);

            await Assert.ThrowsAsync<ArgumentException>(() => JammingTools.RecordJammingIncident(
                mockJammingRepository.Object,
                mockDeviceRepository.Object,
                deviceId,
                start,
                end,
                affectedEventCount: 1,
                averageDegradationDb: 5,
                confidence: JammingConfidenceLevel.Low,
                cancellationToken: CancellationToken.None));

            mockJammingRepository.Verify(r => r.UpsertIncidentAsync(It.IsAny<JammingIncidentRecord>(), It.IsAny<CancellationToken>()), Times.Never);
            mockDeviceRepository.Verify(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RecordJammingIncident_ThrowsWhenDeviceNotFound()
        {
            var deviceId = Guid.NewGuid();
            var mockJammingRepository = new Mock<IJammingRepository>();
            var mockDeviceRepository = new Mock<IDeviceRepository>();

            mockDeviceRepository
                .Setup(r => r.GetAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Device?)null);

            var start = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

            await Assert.ThrowsAsync<InvalidOperationException>(() => JammingTools.RecordJammingIncident(
                mockJammingRepository.Object,
                mockDeviceRepository.Object,
                deviceId,
                start,
                start.AddMinutes(5),
                affectedEventCount: 1,
                averageDegradationDb: 5,
                confidence: JammingConfidenceLevel.Low,
                cancellationToken: CancellationToken.None));

            mockJammingRepository.Verify(r => r.UpsertIncidentAsync(It.IsAny<JammingIncidentRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetJammingStats_WithDeviceId_ReturnsEmpty_WhenNoSummaryExists()
        {
            var deviceId = Guid.NewGuid();
            var mockJammingRepository = new Mock<IJammingRepository>();
            mockJammingRepository
                .Setup(r => r.GetStatsAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((JammingStatsSummary?)null);

            var result = await JammingTools.GetJammingStats(mockJammingRepository.Object, CancellationToken.None, deviceId);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetJammingStats_WithoutDeviceId_ListsAllSummaries()
        {
            var mockJammingRepository = new Mock<IJammingRepository>();
            var summaries = new List<JammingStatsSummary>
            {
                new JammingStatsSummary { Id = Guid.NewGuid(), DeviceId = Guid.NewGuid(), IncidentCount = 2 }
            };
            mockJammingRepository
                .Setup(r => r.ListStatsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(summaries);

            var result = await JammingTools.GetJammingStats(mockJammingRepository.Object, CancellationToken.None, deviceId: null);

            Assert.Same(summaries, result);
        }

        [Fact]
        public async Task RunJammingDetection_ThrowsWhenDeviceNotFound()
        {
            var deviceId = Guid.NewGuid();
            var mockDeviceRepository = new Mock<IDeviceRepository>();
            mockDeviceRepository
                .Setup(r => r.GetAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Device?)null);

            var mockSessionProvider = new Mock<ISessionProvider>();
            var mockDetector = new Mock<VideoForensics.Providers.Ring.Forensics.ISignalAnomalyDetector>();
            var mockJammingRepository = new Mock<IJammingRepository>();
            var logger = new Mock<Microsoft.Extensions.Logging.ILogger<object>>().Object;

            await Assert.ThrowsAsync<InvalidOperationException>(() => JammingTools.RunJammingDetection(
                mockDeviceRepository.Object,
                mockSessionProvider.Object,
                mockDetector.Object,
                mockJammingRepository.Object,
                logger,
                deviceId,
                DateTime.UtcNow.AddDays(-1),
                DateTime.UtcNow,
                CancellationToken.None));

            mockSessionProvider.Verify(s => s.GetSession(), Times.Never);
        }

        [Fact]
        public async Task RunJammingDetection_ThrowsWhenNotAuthenticated()
        {
            var deviceId = Guid.NewGuid();
            var mockDeviceRepository = new Mock<IDeviceRepository>();
            mockDeviceRepository
                .Setup(r => r.GetAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(MakeDevice(deviceId));

            var mockSessionProvider = new Mock<ISessionProvider>();
            mockSessionProvider.Setup(s => s.GetSession()).Returns((Session?)null);

            var mockDetector = new Mock<VideoForensics.Providers.Ring.Forensics.ISignalAnomalyDetector>();
            var mockJammingRepository = new Mock<IJammingRepository>();
            var logger = new Mock<Microsoft.Extensions.Logging.ILogger<object>>().Object;

            await Assert.ThrowsAsync<InvalidOperationException>(() => JammingTools.RunJammingDetection(
                mockDeviceRepository.Object,
                mockSessionProvider.Object,
                mockDetector.Object,
                mockJammingRepository.Object,
                logger,
                deviceId,
                DateTime.UtcNow.AddDays(-1),
                DateTime.UtcNow,
                CancellationToken.None));
        }
    }
}
