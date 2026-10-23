using ICSharpCode.SharpZipLib.Zip;

using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Data.Core.Models;

using Xunit;

namespace VideoForensics.Client.Core.Tests
{
    /// <summary>Tests the case-scoped export: only the selected case's reports and pinned media go in the archive.</summary>
    public class CaseEvidenceExportTests : IDisposable
    {
        private readonly Mock<IMediaItemRepository> _media = new();
        private readonly Mock<IIntegrityVerificationService> _integrity = new();
        private readonly Mock<IActionLogRepository> _actionLog = new();
        private readonly Mock<IExportRecordService> _exportRecords = new();
        private readonly Mock<ICaseRepository> _cases = new();
        private readonly Mock<IReportGenerationService> _reports = new();
        private readonly Mock<IForensicsConfigurationService> _configuration = new();
        private readonly Mock<IStorageLocationProvider> _storage = new();
        private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        private readonly string _defaultExportsRoot;
        private readonly EvidenceExportOrchestrator _orchestrator;

        public CaseEvidenceExportTests()
        {
            _ = Directory.CreateDirectory(_tempDir);
            _defaultExportsRoot = Path.Combine(_tempDir, "default-exports");
            _orchestrator = new EvidenceExportOrchestrator(
                new Mock<ILogger<EvidenceExportOrchestrator>>().Object,
                _media.Object,
                _integrity.Object,
                _actionLog.Object,
                _exportRecords.Object,
                _cases.Object,
                _reports.Object,
                _configuration.Object,
                _storage.Object);

            // The exports folder comes from the database setting; the default root stands in when none is stored.
            _ = _configuration.Setup(c => c.LoadConfigurationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ForensicsConfiguration());
            _ = _storage.Setup(s => s.GetEffectiveRoot(StorageCategory.Exports, It.IsAny<string?>())).Returns(_defaultExportsRoot);

            _ = _integrity.Setup(s => s.VerifyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _ = _actionLog
                .Setup(r => r.GetHistoryForEntityAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
            _ = _exportRecords
                .Setup(s => s.RecordExportAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<IReadOnlyList<(Guid, string)>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ExportRecord { Id = Guid.NewGuid(), ExportedByUserName = "u", ArchiveFileName = "a.zip", ArchiveSha256Hash = "h", AppVersion = "1" });
            _ = _reports.Setup(r => r.BuildEvidenceReviewAsync(It.IsAny<Guid?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(new EvidenceReviewReport());
            _ = _reports.Setup(r => r.BuildForensicAnalysisReportAsync(It.IsAny<Guid?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ForensicAnalysisReport());
            _ = _reports.Setup(r => r.BuildSignalAnomalyReportAsync(It.IsAny<Guid?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(new SignalAnomalyReport());
            _ = _reports.Setup(r => r.BuildAccessControlReportAsync(It.IsAny<Guid?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(new AccessControlReport());
            _ = _reports.Setup(r => r.BuildChainOfCustodyReportAsync(It.IsAny<Guid?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ChainOfCustodyReport());
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        private MediaItem AddMedia(string fileName, Guid? deviceId = null, DateTime? recordedAtUtc = null)
        {
            var item = new MediaItem
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId ?? Guid.NewGuid(),
                FileName = fileName,
                FilePath = Path.Combine(_tempDir, fileName),
                Sha256Hash = "abc",
                MediaFormat = "mp4",
                RecordedAtUtc = recordedAtUtc ?? DateTime.UtcNow,
                DownloadedAtUtc = DateTime.UtcNow
            };
            File.WriteAllText(item.FilePath, "content-" + fileName);
            _ = _media.Setup(m => m.GetAsync(item.Id, It.IsAny<CancellationToken>())).ReturnsAsync(item);
            return item;
        }

        private static CaseItem Pin(Guid caseId, MediaItem media)
        {
            return new()
            {
                Id = Guid.NewGuid(),
                CaseId = caseId,
                Kind = CaseItemKind.Media,
                MediaItemId = media.Id,
                Reason = "r",
                AddedBy = "u"
            };
        }

        private Guid SetUpCase(IReadOnlyList<CaseItem> items, IReadOnlyList<Guid>? devices = null, bool withWindow = true)
        {
            var caseId = Guid.NewGuid();
            _ = _cases.Setup(c => c.GetAsync(caseId, It.IsAny<CancellationToken>())).ReturnsAsync(new ForensicCase
            {
                Id = caseId,
                CaseNumber = "C-1",
                Title = "t",
                CreatedBy = "u",
                ScopeFromUtc = withWindow ? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) : null,
                ScopeToUtc = withWindow ? new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc) : null
            });
            _ = _cases.Setup(c => c.ListItemsAsync(caseId, false, It.IsAny<CancellationToken>())).ReturnsAsync(items);
            _ = _cases.Setup(c => c.GetDeviceIdsAsync(caseId, It.IsAny<CancellationToken>())).ReturnsAsync(devices ?? []);
            return caseId;
        }

        private static List<string> ZipEntries(string path)
        {
            using var zip = new ZipFile(path);
            var names = new List<string>();
            foreach (ZipEntry e in zip)
            {
                names.Add(e.Name);
            }

            return names;
        }

        [Fact]
        public async Task ExportEvidenceAsync_CaseScoped_WritesArchiveToConfiguredExportsLocation()
        {
            // Arrange: the stored ExportsLocation setting must be the root the archive is written under.
            string configuredRoot = Path.Combine(_tempDir, "configured-exports");
            _ = _configuration.Setup(c => c.LoadConfigurationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ForensicsConfiguration { ExportsLocation = configuredRoot });
            _ = _storage.Setup(s => s.GetEffectiveRoot(StorageCategory.Exports, configuredRoot)).Returns(configuredRoot);
            MediaItem pinned = AddMedia("configured-root.mp4");
            Guid caseId = SetUpCase([Pin(Guid.NewGuid(), pinned)]);

            // Act
            ExportResult result = await _orchestrator.ExportEvidenceAsync(caseId, null, null, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.ArchivePath);
            Assert.Equal(Path.GetFullPath(configuredRoot), Path.GetDirectoryName(Path.GetFullPath(result.ArchivePath!)));
        }

        [Fact]
        public async Task ExportEvidenceAsync_CaseScoped_IncludesOnlyThatCasesPinnedMedia()
        {
            MediaItem pinned = AddMedia("pinned.mp4");
            MediaItem other = AddMedia("other-case.mp4");
            var caseId = Guid.NewGuid();
            caseId = SetUpCase([Pin(caseId, pinned)]);

            ExportResult result = await _orchestrator.ExportEvidenceAsync(caseId, null, null, CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(1, result.ItemsIncluded);
            List<string> names = ZipEntries(result.ArchivePath!);
            Assert.Contains("pinned.mp4", names);
            Assert.DoesNotContain("other-case.mp4", names);
            _media.Verify(m => m.GetAsync(other.Id, It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExportEvidenceAsync_CaseScoped_IgnoresEventPins()
        {
            MediaItem pinned = AddMedia("pinned2.mp4");
            Guid caseId = SetUpCase([
                Pin(Guid.Empty, pinned),
                new CaseItem { Id = Guid.NewGuid(), Kind = CaseItemKind.Event, EventId = Guid.NewGuid(), Reason = "r", AddedBy = "u" }
            ]);

            ExportResult result = await _orchestrator.ExportEvidenceAsync(caseId, null, null, CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(1, result.ItemsIncluded);
        }

        [Fact]
        public async Task ExportEvidenceAsync_CaseScoped_IncludesCaseReportsForEachScopeDevice()
        {
            var deviceA = Guid.NewGuid();
            var deviceB = Guid.NewGuid();
            MediaItem pinned = AddMedia("pinned3.mp4", deviceA);
            Guid caseId = SetUpCase([Pin(Guid.Empty, pinned)], [deviceA, deviceB]);

            ExportResult result = await _orchestrator.ExportEvidenceAsync(caseId, null, null, CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            List<string> names = ZipEntries(result.ArchivePath!);
            Assert.Equal(10, names.Count(n => n.StartsWith("reports/", StringComparison.Ordinal)));
            Assert.Contains(names, n => n.Contains("chain-of-custody") && n.Contains(deviceA.ToString()));
            DateTime from = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime to = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
            _reports.Verify(r => r.BuildEvidenceReviewAsync(deviceA, from, to, It.IsAny<CancellationToken>()), Times.Once);
            _reports.Verify(r => r.BuildEvidenceReviewAsync(deviceB, from, to, It.IsAny<CancellationToken>()), Times.Once);
            _reports.Verify(r => r.BuildEvidenceReviewAsync(null, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExportEvidenceAsync_CaseScopedWithNoMedia_StillExportsReportsForScopeDevices()
        {
            Guid caseId = SetUpCase([], [Guid.NewGuid()]);

            ExportResult result = await _orchestrator.ExportEvidenceAsync(caseId, null, null, CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(0, result.ItemsIncluded);
            Assert.Equal(5, ZipEntries(result.ArchivePath!).Count(n => n.StartsWith("reports/", StringComparison.Ordinal)));
        }

        [Fact]
        public async Task ExportEvidenceAsync_NoWindowAndNoPinnedItems_ExportsNoReports()
        {
            Guid caseId = SetUpCase([], [Guid.NewGuid()], withWindow: false);

            ExportResult result = await _orchestrator.ExportEvidenceAsync(caseId, null, null, CancellationToken.None);

            Assert.Null(result.ArchivePath);
            _reports.Verify(r => r.BuildEvidenceReviewAsync(It.IsAny<Guid?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
            _reports.Verify(r => r.BuildChainOfCustodyReportAsync(It.IsAny<Guid?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExportEvidenceAsync_ReportsOnlyCoverDevicesOfPinnedMedia()
        {
            var deviceA = Guid.NewGuid();
            MediaItem pinned = AddMedia("dev-a.mp4", deviceA, new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc));
            Guid caseId = SetUpCase([Pin(Guid.Empty, pinned)]);

            ExportResult result = await _orchestrator.ExportEvidenceAsync(caseId, null, null, CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            _reports.Verify(r => r.BuildEvidenceReviewAsync(deviceA, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
            _reports.Verify(r => r.BuildEvidenceReviewAsync(It.Is<Guid?>(d => d != deviceA), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExportEvidenceAsync_NoWindow_DerivesWindowFromPinnedMediaNotEpoch()
        {
            DateTime rec = new(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);
            MediaItem pinned = AddMedia("no-window.mp4", null, rec);
            Guid caseId = SetUpCase([Pin(Guid.Empty, pinned)], withWindow: false);

            ExportResult result = await _orchestrator.ExportEvidenceAsync(caseId, null, null, CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            _reports.Verify(r => r.BuildEvidenceReviewAsync(It.IsAny<Guid?>(), It.Is<DateTime>(f => f == rec), It.Is<DateTime>(t => t > rec && t < rec.AddDays(1)), It.IsAny<CancellationToken>()), Times.Once);
            _reports.Verify(r => r.BuildEvidenceReviewAsync(It.IsAny<Guid?>(), DateTime.UnixEpoch, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExportEvidenceAsync_CaseScoped_RecordsCaseNumberAsReference()
        {
            MediaItem pinned = AddMedia("pinned4.mp4");
            Guid caseId = SetUpCase([Pin(Guid.Empty, pinned)]);

            _ = await _orchestrator.ExportEvidenceAsync(caseId, "recipient", null, CancellationToken.None);

            _exportRecords.Verify(s => s.RecordExportAsync(It.IsAny<string>(), "C-1", "recipient", It.IsAny<string>(), It.IsAny<string>(), false, It.IsAny<IReadOnlyList<(Guid, string)>>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExportEvidenceAsync_CaseScoped_UnknownCase_ReturnsFailure()
        {
            var unknown = Guid.NewGuid();
            _ = _cases.Setup(c => c.GetAsync(unknown, It.IsAny<CancellationToken>())).ReturnsAsync((ForensicCase?)null);

            ExportResult result = await _orchestrator.ExportEvidenceAsync(unknown, null, null, CancellationToken.None);

            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
            _media.Verify(m => m.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
