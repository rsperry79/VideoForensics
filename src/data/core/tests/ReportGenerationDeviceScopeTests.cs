using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Models;
using VideoForensics.Data.Core.Services;

using Xunit;

using INotificationDispatcher = VideoForensics.Core.Providers.Common.Contracts.INotificationDispatcher;

namespace VideoForensics.Data.Core.Tests
{
    /// <summary>Device-scoped audit reports: the trail narrows to one device, the chain verdict stays global, and a broken chain alerts once.</summary>
    public class ReportGenerationDeviceScopeTests
    {
        private static readonly DateTime From = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime To = new(2024, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        private readonly Mock<IActionLogRepository> _actionLog = new();
        private readonly Mock<INotificationDispatcher> _dispatcher = new();

        private ReportGenerationService CreateService()
        {
            var storage = new Mock<IStorageLocationProvider>();
            _ = storage.Setup(x => x.GetDefaultRoot(It.IsAny<StorageCategory>())).Returns(Path.GetTempPath());
            _ = storage.Setup(x => x.GetEffectiveRoot(It.IsAny<StorageCategory>(), It.IsAny<string?>())).Returns(Path.GetTempPath());
            var integrity = new Mock<IIntegrityRecordRepository>();
            _ = integrity.Setup(x => x.GetLatestByMediaItemIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

            return new ReportGenerationService(
                new Mock<IMediaItemRepository>().Object,
                new Mock<IDeviceRepository>().Object,
                new Mock<IDownloadEventRepository>().Object,
                _actionLog.Object,
                new Mock<IEventRepository>().Object,
                new Mock<IJammingRepository>().Object,
                integrity.Object,
                new Mock<ILogger<ReportGenerationService>>().Object,
                storage.Object,
                _dispatcher.Object);
        }

        private static ActionLogEntry Entry(string action, Guid? entityId, string? details, DateTime at)
        {
            return new()
            {
                Id = Guid.NewGuid(),
                Actor = "operator",
                ActorType = ActorType.Human,
                Action = action,
                EntityType = "Device",
                EntityId = entityId,
                DetailsJson = details,
                TimestampUtc = at,
                EntryHash = "hash",
            };
        }

        [Fact]
        public async Task BuildChainOfCustodyReportAsync_WithDeviceId_KeepsOnlyThatDevicesEntries()
        {
            var device = Guid.NewGuid();
            var other = Guid.NewGuid();
            var day = new DateTime(2024, 1, 10, 0, 0, 0, DateTimeKind.Utc);
            _ = _actionLog.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
            [
                Entry("DeviceRegistered", device, null, day),
                Entry("MediaDownloaded", null, $"{{\"deviceId\":\"{device:D}\"}}", day.AddHours(1)),
                Entry("DeviceRegistered", other, null, day.AddHours(2)),
            ]);
            _ = _actionLog.Setup(x => x.VerifyChainIntegrityAsync(It.IsAny<CancellationToken>())).ReturnsAsync(ChainIntegrityResult.Valid(2));

            ChainOfCustodyReport report = await CreateService().BuildChainOfCustodyReportAsync(device, From, To, CancellationToken.None);

            Assert.Equal(2, report.AuditTrail.Count);
            Assert.DoesNotContain(report.AuditTrail, e => e.EntityId == other);
        }

        [Fact]
        public async Task BuildChainOfCustodyReportAsync_WithDeviceId_VerdictStaysGlobalAndFailureIsReported()
        {
            var device = Guid.NewGuid();
            _ = _actionLog.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
            _ = _actionLog.Setup(x => x.VerifyChainIntegrityAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new ChainIntegrityResult(false, 1, ChainBreakKind.EntryHashMismatch, Guid.NewGuid(), DateTime.UtcNow));

            ChainOfCustodyReport report = await CreateService().BuildChainOfCustodyReportAsync(device, From, To, CancellationToken.None);

            Assert.False(report.ChainIntegrityVerified);
            _actionLog.Verify(x => x.VerifyChainIntegrityAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task BuildAccessControlReportAsync_WithDeviceId_KeepsOnlyThatDevicesEvents()
        {
            var device = Guid.NewGuid();
            var other = Guid.NewGuid();
            var day = new DateTime(2024, 1, 10, 0, 0, 0, DateTimeKind.Utc);
            _ = _actionLog.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
            [
                Entry("MediaViewed", device, null, day),
                Entry("MediaViewed", other, null, day.AddHours(1)),
            ]);

            AccessControlReport report = await CreateService().BuildAccessControlReportAsync(device, From, To, CancellationToken.None);

            _ = Assert.Single(report.AccessEvents);
            Assert.Equal(device, report.AccessEvents[0].EntityId);
        }

        [Fact]
        public async Task BuildChainOfCustodyReportAsync_BrokenChainTwiceWithinWindow_AlertsOnce()
        {
            _ = _actionLog.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
            _ = _actionLog.Setup(x => x.VerifyChainIntegrityAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new ChainIntegrityResult(false, 1, ChainBreakKind.EntryHashMismatch, Guid.NewGuid(), DateTime.UtcNow));
            ReportGenerationService service = CreateService();

            _ = await service.BuildChainOfCustodyReportAsync(Guid.NewGuid(), From, To, CancellationToken.None);
            _ = await service.BuildChainOfCustodyReportAsync(Guid.NewGuid(), From, To, CancellationToken.None);

            _dispatcher.Verify(
                d => d.DispatchAsync(It.IsAny<VideoForensics.Core.Providers.Common.Contracts.NotificationEvent>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
