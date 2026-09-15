using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Core.Services;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Core.Tests
{
    public class ForensicsConfigurationServiceTests
    {
        private readonly Mock<ILogger<ForensicsConfigurationService>> _loggerMock;
        private readonly Mock<IAppSettingRepository> _settingRepositoryMock;
        private readonly ForensicsConfigurationService _service;

        public ForensicsConfigurationServiceTests()
        {
            _loggerMock = new Mock<ILogger<ForensicsConfigurationService>>();
            _settingRepositoryMock = new Mock<IAppSettingRepository>();
            _service = new ForensicsConfigurationService(_loggerMock.Object, _settingRepositoryMock.Object);
        }

        [Fact]
        public async Task LoadConfigurationAsync_EmptyRepository_ReturnsDefaults()
        {
            _ = _settingRepositoryMock
                .Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);

            IForensicsConfiguration config = await _service.LoadConfigurationAsync("test-path");

            Assert.NotNull(config);
            Assert.True(config.EnableForensicAnalysisReports);
            Assert.True(config.EnableSignalAnomalyReports);
            Assert.Equal("json", config.ReportOutputFormat);
            Assert.Equal(180, config.RetentionDaysDefault);
            Assert.Equal(10, config.MaxConcurrentDownloads);
        }

        [Fact]
        public async Task LoadConfigurationAsync_WithStoredBooleanSettings_LoadsCorrectly()
        {
            _ = _settingRepositoryMock
                .Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);

            _ = _settingRepositoryMock
                .Setup(r => r.GetAsync("EnableForensicAnalysisReports", It.IsAny<CancellationToken>()))
                .ReturnsAsync("false");

            _ = _settingRepositoryMock
                .Setup(r => r.GetAsync("EnableSignalAnomalyReports", It.IsAny<CancellationToken>()))
                .ReturnsAsync("true");

            _ = _settingRepositoryMock
                .Setup(r => r.GetAsync("EnableForensicAnalysisReports", It.IsAny<CancellationToken>()))
                .ReturnsAsync("false");

            _ = _settingRepositoryMock
                .Setup(r => r.GetAsync("EnableSignalAnomalyReports", It.IsAny<CancellationToken>()))
                .ReturnsAsync("true");

            IForensicsConfiguration config = await _service.LoadConfigurationAsync("test-path");

            Assert.NotNull(config);
            Assert.False(config.EnableForensicAnalysisReports);
            Assert.True(config.EnableSignalAnomalyReports);
        }

        [Fact]
        public async Task LoadConfigurationAsync_WithStoredIntegerSettings_LoadsCorrectly()
        {
            _ = _settingRepositoryMock
                .Setup(r => r.GetAsync("RetentionDaysDefault", It.IsAny<CancellationToken>()))
                .ReturnsAsync("365");

            _ = _settingRepositoryMock
                .Setup(r => r.GetAsync("MaxConcurrentDownloads", It.IsAny<CancellationToken>()))
                .ReturnsAsync("5");

            _ = _settingRepositoryMock
                .Setup(r => r.GetAsync(It.IsNotIn("RetentionDaysDefault", "MaxConcurrentDownloads"), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);

            IForensicsConfiguration config = await _service.LoadConfigurationAsync("test-path");

            Assert.NotNull(config);
            Assert.Equal(365, config.RetentionDaysDefault);
            Assert.Equal(5, config.MaxConcurrentDownloads);
        }

        [Fact]
        public async Task LoadConfigurationAsync_WithStoredStringSettings_LoadsCorrectly()
        {
            _ = _settingRepositoryMock
                .Setup(r => r.GetAsync("DownloadLocation", It.IsAny<CancellationToken>()))
                .ReturnsAsync("C:\\Downloads\\Videos");

            _ = _settingRepositoryMock
                .Setup(r => r.GetAsync("ReportOutputFormat", It.IsAny<CancellationToken>()))
                .ReturnsAsync("xml");

            _ = _settingRepositoryMock
                .Setup(r => r.GetAsync(It.IsNotIn("DownloadLocation", "ReportOutputFormat"), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);

            IForensicsConfiguration config = await _service.LoadConfigurationAsync("test-path");

            Assert.NotNull(config);
            Assert.Equal("C:\\Downloads\\Videos", config.DownloadLocation);
            Assert.Equal("xml", config.ReportOutputFormat);
        }

        [Fact]
        public async Task LoadConfigurationAsync_WithStoredEnumSettings_LoadsCorrectly()
        {
            _ = _settingRepositoryMock
                .Setup(r => r.GetAsync("RedactionLevel", It.IsAny<CancellationToken>()))
                .ReturnsAsync("Heavy");

            _ = _settingRepositoryMock
                .Setup(r => r.GetAsync("KeyStorageProvider", It.IsAny<CancellationToken>()))
                .ReturnsAsync("Tpm");

            _ = _settingRepositoryMock
                .Setup(r => r.GetAsync(It.IsNotIn("RedactionLevel", "KeyStorageProvider"), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);

            IForensicsConfiguration config = await _service.LoadConfigurationAsync("test-path");

            Assert.NotNull(config);
            Assert.Equal(RedactionLevel.Heavy, config.RedactionLevel);
            Assert.Equal(KeyStorageProvider.Tpm, config.KeyStorageProvider);
        }

        [Fact]
        public async Task LoadConfigurationAsync_WithStoredGuidSettings_LoadsCorrectly()
        {
            var accountId = Guid.NewGuid();

            _ = _settingRepositoryMock
                .Setup(r => r.GetAsync("ActiveProviderAccountId", It.IsAny<CancellationToken>()))
                .ReturnsAsync(accountId.ToString());

            _ = _settingRepositoryMock
                .Setup(r => r.GetAsync(It.IsNotIn("ActiveProviderAccountId"), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);

            IForensicsConfiguration config = await _service.LoadConfigurationAsync("test-path");

            Assert.NotNull(config);
            Assert.Equal(accountId, config.ActiveProviderAccountId);
        }

        [Fact]
        public async Task LoadConfigurationAsync_LoadDatabaseFailure_UsesDefaults()
        {
            _ = _settingRepositoryMock
                .Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Database connection failed"));

            IForensicsConfiguration config = await _service.LoadConfigurationAsync("test-path");

            Assert.NotNull(config);
            // Should have defaults, not throw
            Assert.True(config.EnableForensicAnalysisReports);
        }

        [Fact]
        public async Task SaveConfigurationAsync_SavesAllSettings()
        {
            var config = new ForensicsConfiguration
            {
                EnableForensicAnalysisReports = false,
                RetentionDaysDefault = 90,
                MaxConcurrentDownloads = 3,
                DownloadLocation = "/downloads",
                ReportOutputFormat = "csv",
                RedactionLevel = RedactionLevel.Light,
                KeyStorageProvider = KeyStorageProvider.Dpapi
            };

            await _service.SaveConfigurationAsync(config);

            _settingRepositoryMock.Verify(
                r => r.SetAsync("EnableForensicAnalysisReports", "False", It.IsAny<CancellationToken>()),
                Times.Once);

            _settingRepositoryMock.Verify(
                r => r.SetAsync("RetentionDaysDefault", "90", It.IsAny<CancellationToken>()),
                Times.Once);

            _settingRepositoryMock.Verify(
                r => r.SetAsync("MaxConcurrentDownloads", "3", It.IsAny<CancellationToken>()),
                Times.Once);

            _settingRepositoryMock.Verify(
                r => r.SetAsync("DownloadLocation", "/downloads", It.IsAny<CancellationToken>()),
                Times.Once);

            _settingRepositoryMock.Verify(
                r => r.SetAsync("ReportOutputFormat", "csv", It.IsAny<CancellationToken>()),
                Times.Once);

            _settingRepositoryMock.Verify(
                r => r.SetAsync("RedactionLevel", "Light", It.IsAny<CancellationToken>()),
                Times.Once);

            _settingRepositoryMock.Verify(
                r => r.SetAsync("KeyStorageProvider", "Dpapi", It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task SaveConfigurationAsync_SavesAllSmtpSettings()
        {
            var config = new ForensicsConfiguration
            {
                EnableEmailNotifications = true,
                SmtpHost = "smtp.gmail.com",
                SmtpPort = 587,
                SmtpUseTls = true,
                SmtpUsername = "user@example.com",
                SmtpFromAddress = "noreply@example.com",
                NotificationRecipientEmail = "admin@example.com"
            };

            await _service.SaveConfigurationAsync(config);

            _settingRepositoryMock.Verify(
                r => r.SetAsync("EnableEmailNotifications", "True", It.IsAny<CancellationToken>()),
                Times.Once);

            _settingRepositoryMock.Verify(
                r => r.SetAsync("SmtpHost", "smtp.gmail.com", It.IsAny<CancellationToken>()),
                Times.Once);

            _settingRepositoryMock.Verify(
                r => r.SetAsync("SmtpPort", "587", It.IsAny<CancellationToken>()),
                Times.Once);

            _settingRepositoryMock.Verify(
                r => r.SetAsync("SmtpUseTls", "True", It.IsAny<CancellationToken>()),
                Times.Once);

            _settingRepositoryMock.Verify(
                r => r.SetAsync("SmtpUsername", "user@example.com", It.IsAny<CancellationToken>()),
                Times.Once);

            _settingRepositoryMock.Verify(
                r => r.SetAsync("SmtpFromAddress", "noreply@example.com", It.IsAny<CancellationToken>()),
                Times.Once);

            _settingRepositoryMock.Verify(
                r => r.SetAsync("NotificationRecipientEmail", "admin@example.com", It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task SaveConfigurationAsync_SavesNetworkAndServerSettings()
        {
            var config = new ForensicsConfiguration
            {
                ConfiguredNetworkTier = NetworkTier.Internet,
                InternetServerUrl = "https://example.com:8443"
            };

            await _service.SaveConfigurationAsync(config);

            _settingRepositoryMock.Verify(
                r => r.SetAsync("ConfiguredNetworkTier", "Internet", It.IsAny<CancellationToken>()),
                Times.Once);

            _settingRepositoryMock.Verify(
                r => r.SetAsync("InternetServerUrl", "https://example.com:8443", It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task SaveConfigurationAsync_SavesUniviewSettings()
        {
            var config = new ForensicsConfiguration
            {
                UniviewNvrHost = "192.168.1.100",
                UniviewFfmpegPath = "/usr/bin/ffmpeg"
            };

            await _service.SaveConfigurationAsync(config);

            _settingRepositoryMock.Verify(
                r => r.SetAsync("UniviewNvrHost", "192.168.1.100", It.IsAny<CancellationToken>()),
                Times.Once);

            _settingRepositoryMock.Verify(
                r => r.SetAsync("UniviewFfmpegPath", "/usr/bin/ffmpeg", It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task SaveConfigurationAsync_WithNullStringValues_SavesEmptyString()
        {
            var config = new ForensicsConfiguration
            {
                DownloadLocation = null,
                InternetServerUrl = null
            };

            await _service.SaveConfigurationAsync(config);

            _settingRepositoryMock.Verify(
                r => r.SetAsync("DownloadLocation", "", It.IsAny<CancellationToken>()),
                Times.Once);

            _settingRepositoryMock.Verify(
                r => r.SetAsync("InternetServerUrl", "", It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task SaveConfigurationAsync_WithNullGuid_SavesEmptyString()
        {
            var config = new ForensicsConfiguration
            {
                ActiveProviderAccountId = null
            };

            await _service.SaveConfigurationAsync(config);

            _settingRepositoryMock.Verify(
                r => r.SetAsync("ActiveProviderAccountId", "", It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task SaveConfigurationAsync_SavesWithCancellationToken()
        {
            var config = new ForensicsConfiguration();
            CancellationToken ct = CancellationToken.None;

            await _service.SaveConfigurationAsync(config, ct);

            _settingRepositoryMock.Verify(
                r => r.SetAsync(It.IsAny<string>(), It.IsAny<string>(), ct),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task LoadConfigurationAsync_LoadsAllHealthAndSyncSettings()
        {
            _ = _settingRepositoryMock
                .Setup(r => r.GetAsync("EnableHealthSync", It.IsAny<CancellationToken>()))
                .ReturnsAsync("false");

            _ = _settingRepositoryMock
                .Setup(r => r.GetAsync("EnableMdnsAdvertisement", It.IsAny<CancellationToken>()))
                .ReturnsAsync("false");

            _ = _settingRepositoryMock
                .Setup(r => r.GetAsync(It.IsNotIn("EnableHealthSync", "EnableMdnsAdvertisement"), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);

            IForensicsConfiguration config = await _service.LoadConfigurationAsync("test-path");

            Assert.NotNull(config);
            Assert.False(config.EnableHealthSync);
            Assert.False(config.EnableMdnsAdvertisement);
        }

        [Fact]
        public async Task LoadConfigurationAsync_LoadsAllReportSettings()
        {
            _ = _settingRepositoryMock
                .Setup(r => r.GetAsync("EnableForensicAnalysisReports", It.IsAny<CancellationToken>()))
                .ReturnsAsync("false");

            _ = _settingRepositoryMock
                .Setup(r => r.GetAsync("EnableChainOfCustodyReports", It.IsAny<CancellationToken>()))
                .ReturnsAsync("false");

            _ = _settingRepositoryMock
                .Setup(r => r.GetAsync("EnableEvidenceValidationReports", It.IsAny<CancellationToken>()))
                .ReturnsAsync("true");

            _ = _settingRepositoryMock
                .Setup(r => r.GetAsync("EnableAccessControlMonitoring", It.IsAny<CancellationToken>()))
                .ReturnsAsync("true");

            _ = _settingRepositoryMock
                .Setup(r => r.GetAsync("EnablePiiRedaction", It.IsAny<CancellationToken>()))
                .ReturnsAsync("false");

            _ = _settingRepositoryMock
                .Setup(r => r.GetAsync(It.IsNotIn("EnableForensicAnalysisReports", "EnableChainOfCustodyReports", "EnableEvidenceValidationReports", "EnableAccessControlMonitoring", "EnablePiiRedaction"), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);

            IForensicsConfiguration config = await _service.LoadConfigurationAsync("test-path");

            Assert.NotNull(config);
            Assert.False(config.EnableForensicAnalysisReports);
            Assert.False(config.EnableChainOfCustodyReports);
            Assert.True(config.EnableEvidenceValidationReports);
            Assert.True(config.EnableAccessControlMonitoring);
            Assert.False(config.EnablePiiRedaction);
        }

        [Fact]
        public async Task SaveConfigurationAsync_SavesAllReportSettings()
        {
            var config = new ForensicsConfiguration
            {
                EnableForensicAnalysisReports = true,
                EnableSignalAnomalyReports = false,
                EnableChainOfCustodyReports = true,
                EnableEvidenceValidationReports = false,
                EnableAccessControlMonitoring = true,
                EnablePiiRedaction = false
            };

            await _service.SaveConfigurationAsync(config);

            _settingRepositoryMock.Verify(
                r => r.SetAsync("EnableForensicAnalysisReports", "True", It.IsAny<CancellationToken>()),
                Times.Once);

            _settingRepositoryMock.Verify(
                r => r.SetAsync("EnableSignalAnomalyReports", "False", It.IsAny<CancellationToken>()),
                Times.Once);

            _settingRepositoryMock.Verify(
                r => r.SetAsync("EnableChainOfCustodyReports", "True", It.IsAny<CancellationToken>()),
                Times.Once);

            _settingRepositoryMock.Verify(
                r => r.SetAsync("EnableEvidenceValidationReports", "False", It.IsAny<CancellationToken>()),
                Times.Once);

            _settingRepositoryMock.Verify(
                r => r.SetAsync("EnableAccessControlMonitoring", "True", It.IsAny<CancellationToken>()),
                Times.Once);

            _settingRepositoryMock.Verify(
                r => r.SetAsync("EnablePiiRedaction", "False", It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public void Constructor_WithNullSettingRepository_ThrowsArgumentNullException()
        {
            _ = Assert.Throws<ArgumentNullException>(() =>
                new ForensicsConfigurationService(_loggerMock.Object, null!));
        }

        [Fact]
        public async Task LoadConfigurationAsync_WithEmptyStringValues_ReturnsDefaults()
        {
            _ = _settingRepositoryMock
                .Setup(r => r.GetAsync("DownloadStartDate", It.IsAny<CancellationToken>()))
                .ReturnsAsync("");

            _ = _settingRepositoryMock
                .Setup(r => r.GetAsync(It.IsNotIn("DownloadStartDate"), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);

            IForensicsConfiguration config = await _service.LoadConfigurationAsync("test-path");

            Assert.NotNull(config);
            Assert.Equal("", config.DownloadStartDate);
        }
    }
}
