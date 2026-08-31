using Microsoft.Extensions.Logging;
using Moq;
using VideoForensics.Client.Common;
using VideoForensics.Client.Core.Tools;
using VideoForensics.Data.Common.Contracts;
using Xunit;

namespace VideoForensics.Client.Core.Tests
{
    public class ConfigToolsOrchestratorTests
    {
        private readonly Mock<ILogger<ConfigToolsOrchestrator>> _loggerMock;
        private readonly Mock<IForensicsConfigurationService> _configServiceMock;
        private readonly Mock<IAppSettingRepository> _settingRepositoryMock;
        private readonly ConfigToolsOrchestrator _orchestrator;

        public ConfigToolsOrchestratorTests()
        {
            _loggerMock = new Mock<ILogger<ConfigToolsOrchestrator>>();
            _configServiceMock = new Mock<IForensicsConfigurationService>();
            _settingRepositoryMock = new Mock<IAppSettingRepository>();
            _orchestrator = new ConfigToolsOrchestrator(
                _loggerMock.Object,
                _configServiceMock.Object,
                _settingRepositoryMock.Object);
        }

        [Fact]
        public async Task SetRetentionDaysAsync_RejectsZeroOrNegative()
        {
            var config = new ForensicsConfiguration();

            var result = await _orchestrator.SetRetentionDaysAsync(config, 0);

            Assert.False(result.Success);
            Assert.Contains("greater than 0", result.Message);
            _configServiceMock.Verify(
                s => s.SaveConfigurationAsync(It.IsAny<IForensicsConfiguration>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task SetRetentionDaysAsync_AcceptsPositiveValue()
        {
            var config = new ForensicsConfiguration();

            var result = await _orchestrator.SetRetentionDaysAsync(config, 30, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(30, config.RetentionDaysDefault);
            _configServiceMock.Verify(
                s => s.SaveConfigurationAsync(config, CancellationToken.None),
                Times.Once);
        }

        [Fact]
        public async Task SetMaxConcurrentDownloadsAsync_RejectsZeroOrNegative()
        {
            var config = new ForensicsConfiguration();

            var result = await _orchestrator.SetMaxConcurrentDownloadsAsync(config, 0);

            Assert.False(result.Success);
            Assert.Contains("at least 1", result.Message);
            _configServiceMock.Verify(
                s => s.SaveConfigurationAsync(It.IsAny<IForensicsConfiguration>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task SetMaxConcurrentDownloadsAsync_AcceptsPositiveValue()
        {
            var config = new ForensicsConfiguration();

            var result = await _orchestrator.SetMaxConcurrentDownloadsAsync(config, 5, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(5, config.MaxConcurrentDownloads);
            _configServiceMock.Verify(
                s => s.SaveConfigurationAsync(config, CancellationToken.None),
                Times.Once);
        }

        [Fact]
        public async Task SetDownloadLocationAsync_CreatesDirectoryAndSaves()
        {
            var config = new ForensicsConfiguration();
            var tempDir = Path.Combine(Path.GetTempPath(), $"videoforensics-test-{Guid.NewGuid()}");

            try
            {
                var result = await _orchestrator.SetDownloadLocationAsync(config, tempDir, CancellationToken.None);

                Assert.True(result.Success);
                Assert.Equal(tempDir, config.DownloadLocation);
                Assert.True(Directory.Exists(tempDir));
                _configServiceMock.Verify(
                    s => s.SaveConfigurationAsync(config, CancellationToken.None),
                    Times.Once);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir);
            }
        }

        [Fact]
        public async Task SetReportEnabledAsync_AcceptsValidReportTypes()
        {
            var config = new ForensicsConfiguration { EnableForensicAnalysisReports = false };
            var reportTypes = new[] { "ForensicAnalysis", "SignalAnomaly", "ChainOfCustody", "EvidenceValidation", "AccessControl" };

            foreach (var reportType in reportTypes)
            {
                var result = await _orchestrator.SetReportEnabledAsync(config, reportType, true, CancellationToken.None);
                Assert.True(result.Success, $"Report type {reportType} should be valid");
            }
        }

        [Fact]
        public async Task SetReportEnabledAsync_RejectsInvalidReportType()
        {
            var config = new ForensicsConfiguration();

            var result = await _orchestrator.SetReportEnabledAsync(config, "InvalidReport", true);

            Assert.False(result.Success);
            Assert.Contains("Unknown report type", result.Message);
        }

        [Fact]
        public async Task SetRedactionLevelAsync_UpdatesConfiguration()
        {
            var config = new ForensicsConfiguration { RedactionLevel = RedactionLevel.None };

            var result = await _orchestrator.SetRedactionLevelAsync(config, RedactionLevel.Heavy, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(RedactionLevel.Heavy, config.RedactionLevel);
            _configServiceMock.Verify(
                s => s.SaveConfigurationAsync(config, CancellationToken.None),
                Times.Once);
        }

        [Fact]
        public async Task SetKeyStorageProviderAsync_UpdatesConfiguration()
        {
            var config = new ForensicsConfiguration { KeyStorageProvider = KeyStorageProvider.Auto };

            var result = await _orchestrator.SetKeyStorageProviderAsync(config, KeyStorageProvider.Tpm, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(KeyStorageProvider.Tpm, config.KeyStorageProvider);
            _configServiceMock.Verify(
                s => s.SaveConfigurationAsync(config, CancellationToken.None),
                Times.Once);
        }

        [Fact]
        public async Task SetLoggingLevelAsync_UpdatesConfiguration()
        {
            var config = new ForensicsConfiguration();

            var result = await _orchestrator.SetLoggingLevelAsync(config, "Debug", CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("Debug", config.LogLevel);
            _configServiceMock.Verify(
                s => s.SaveConfigurationAsync(config, CancellationToken.None),
                Times.Once);
        }

        [Theory]
        [InlineData("json")]
        [InlineData("xml")]
        [InlineData("csv")]
        public async Task SetReportFormatAsync_AcceptsValidFormats(string format)
        {
            var config = new ForensicsConfiguration();

            var result = await _orchestrator.SetReportFormatAsync(config, format, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(format, config.ReportOutputFormat);
        }

        [Fact]
        public async Task SetReportFormatAsync_RejectsInvalidFormat()
        {
            var config = new ForensicsConfiguration();

            var result = await _orchestrator.SetReportFormatAsync(config, "invalid");

            Assert.False(result.Success);
            Assert.Contains("json, xml, or csv", result.Message);
        }

        [Fact]
        public async Task FactoryResetAsync_DeletesDownloadAndDatabaseDirectories()
        {
            // Uses the downloadDirOverride/dbPathOverride parameters to point at throwaway temp
            // paths - FactoryResetAsync's defaults are the REAL production download folder and the
            // REAL live application database, and must never be exercised by a test.
            var downloadDir = Path.Combine(Path.GetTempPath(), $"VideoForensics-FactoryResetTest-{Guid.NewGuid():N}");
            var dbDir = Path.Combine(Path.GetTempPath(), $"VideoForensics-FactoryResetTest-Db-{Guid.NewGuid():N}");
            var dbPath = Path.Combine(dbDir, "videoforensics.db");

            try
            {
                // Create test directories and files
                Directory.CreateDirectory(downloadDir);
                Directory.CreateDirectory(dbDir);
                File.WriteAllText(dbPath, "test");

                Assert.True(Directory.Exists(downloadDir));
                Assert.True(File.Exists(dbPath));

                var result = await _orchestrator.FactoryResetAsync(downloadDirOverride: downloadDir, dbPathOverride: dbPath);

                Assert.True(result.Success);
                Assert.False(Directory.Exists(downloadDir));
                Assert.False(File.Exists(dbPath));
            }
            finally
            {
                if (Directory.Exists(downloadDir))
                    Directory.Delete(downloadDir, recursive: true);
                if (Directory.Exists(dbDir))
                    Directory.Delete(dbDir, recursive: true);
            }
        }
    }
}
