using Moq;
using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class AuthMethodSettingsServiceTests
    {
        private readonly Mock<IAppSettingRepository> _mockSettings;
        private readonly AuthMethodSettingsService _service;

        public AuthMethodSettingsServiceTests()
        {
            _mockSettings = new Mock<IAppSettingRepository>();
            _service = new AuthMethodSettingsService(_mockSettings.Object);
        }

        [Fact]
        public async Task GetAsync_WithDefaults_ReturnsBothEnabled()
        {
            // Arrange
            _mockSettings.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);

            // Act
            var result = await _service.GetAsync(CancellationToken.None);

            // Assert
            Assert.True(result.PasswordEnabled);
            Assert.True(result.PasskeyEnabled);
        }

        [Fact]
        public async Task GetAsync_WithPasswordDisabled_ReturnsCorrectValues()
        {
            // Arrange
            _mockSettings.Setup(s => s.GetAsync("AuthPasswordEnabled", It.IsAny<CancellationToken>()))
                .ReturnsAsync("false");
            _mockSettings.Setup(s => s.GetAsync("AuthPasskeyEnabled", It.IsAny<CancellationToken>()))
                .ReturnsAsync("true");

            // Act
            var result = await _service.GetAsync(CancellationToken.None);

            // Assert
            Assert.False(result.PasswordEnabled);
            Assert.True(result.PasskeyEnabled);
        }

        [Fact]
        public async Task UpdateAsync_WithValidUpdate_PersistsSettings()
        {
            // Arrange
            _mockSettings.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);
            _mockSettings.Setup(s => s.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var request = new UpdateAuthMethodSettingsRequest(PasswordEnabled: false, PasskeyEnabled: true);

            // Act
            var result = await _service.UpdateAsync(request, CancellationToken.None);

            // Assert
            Assert.True(result);
            _mockSettings.Verify(s => s.SetAsync("AuthPasswordEnabled", "False", It.IsAny<CancellationToken>()), Times.Once);
            _mockSettings.Verify(s => s.SetAsync("AuthPasskeyEnabled", "True", It.IsAny<CancellationToken>()), Times.Once);
            _mockSettings.Verify(s => s.SetAsync("AuthMethodsUpdatedAtUtc", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_WhenBothMethodsDisabled_ReturnsFalse()
        {
            // Arrange
            _mockSettings.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);

            var request = new UpdateAuthMethodSettingsRequest(PasswordEnabled: false, PasskeyEnabled: false);

            // Act
            var result = await _service.UpdateAsync(request, CancellationToken.None);

            // Assert
            Assert.False(result);
            _mockSettings.Verify(s => s.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UpdateAsync_WithPartialUpdate_PersistsOnlyChangedField()
        {
            // Arrange
            _mockSettings.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);
            _mockSettings.Setup(s => s.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var request = new UpdateAuthMethodSettingsRequest(PasswordEnabled: false);

            // Act
            var result = await _service.UpdateAsync(request, CancellationToken.None);

            // Assert
            Assert.True(result);
            _mockSettings.Verify(s => s.SetAsync("AuthPasswordEnabled", "False", It.IsAny<CancellationToken>()), Times.Once);
            _mockSettings.Verify(s => s.SetAsync("AuthPasskeyEnabled", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            _mockSettings.Verify(s => s.SetAsync("AuthMethodsUpdatedAtUtc", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetLastUpdatedAtUtcAsync_WhenNeverUpdated_ReturnsMinValue()
        {
            // Arrange
            _mockSettings.Setup(s => s.GetAsync("AuthMethodsUpdatedAtUtc", It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);

            // Act
            var result = await _service.GetLastUpdatedAtUtcAsync(CancellationToken.None);

            // Assert
            Assert.Equal(DateTime.MinValue, result);
        }

        [Fact]
        public async Task GetLastUpdatedAtUtcAsync_WithValidTimestamp_ReturnsPersistedTime()
        {
            // Arrange
            var expectedTime = new DateTime(2026, 10, 2, 14, 30, 0, DateTimeKind.Utc);
            var isoString = expectedTime.ToString("O");
            _mockSettings.Setup(s => s.GetAsync("AuthMethodsUpdatedAtUtc", It.IsAny<CancellationToken>()))
                .ReturnsAsync(isoString);

            // Act
            var result = await _service.GetLastUpdatedAtUtcAsync(CancellationToken.None);

            // Assert
            Assert.Equal(expectedTime, result);
        }

        [Fact]
        public async Task IsEnabledAsync_WithPasswordMethod_ChecksPasswordEnabled()
        {
            // Arrange
            _mockSettings.Setup(s => s.GetAsync("AuthPasswordEnabled", It.IsAny<CancellationToken>()))
                .ReturnsAsync("false");

            // Act
            var result = await _service.IsEnabledAsync("password", CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsEnabledAsync_WithPasskeyMethod_ChecksPasskeyEnabled()
        {
            // Arrange
            _mockSettings.Setup(s => s.GetAsync("AuthPasskeyEnabled", It.IsAny<CancellationToken>()))
                .ReturnsAsync("true");

            // Act
            var result = await _service.IsEnabledAsync("passkey", CancellationToken.None);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsEnabledAsync_WithCaseInsensitiveMethod_ReturnsCorrectValue()
        {
            // Arrange
            _mockSettings.Setup(s => s.GetAsync("AuthPasswordEnabled", It.IsAny<CancellationToken>()))
                .ReturnsAsync("true");

            // Act
            var result = await _service.IsEnabledAsync("PASSWORD", CancellationToken.None);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsEnabledAsync_WithUnknownMethod_ReturnsFalse()
        {
            // Act
            var result = await _service.IsEnabledAsync("unknown", CancellationToken.None);

            // Assert
            Assert.False(result);
        }
    }
}
