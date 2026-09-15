using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Providers.Uniview.Services;

using Xunit;

namespace VideoForensics.Providers.Uniview.Tests
{
    /// <summary>
    /// Tests for UniviewAuthService. Tests the "not authenticated" paths and state tracking.
    /// Live UniviewClient HTTP/TCP calls are not tested here (would require a live device).
    /// </summary>
    public class UniviewAuthServiceTests
    {
        [Fact]
        public void UniviewAuthService_GetAuthStatus_NotAuthenticated_ReturnsNotAuthenticatedMessage()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<UniviewAuthService>>();
            var mockSessionProvider = new Mock<IUniviewSessionProvider>();
            var mockConfig = new Mock<IForensicsConfiguration>();
            var mockCredentialRepository = new Mock<ICredentialRepository>();

            // Session provider returns null (not authenticated)
            _ = mockSessionProvider.Setup(s => s.GetClient()).Returns((UniviewClient?)null);

            var service = new UniviewAuthService(
                mockLogger.Object,
                mockSessionProvider.Object,
                mockConfig.Object,
                mockCredentialRepository.Object);

            // Act
            string status = service.GetAuthStatus();

            // Assert
            Assert.Equal("Not authenticated", status);
        }

        [Fact]
        public void UniviewAuthService_GetAuthStatus_Authenticated_ReturnsAuthenticatedMessage()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<UniviewAuthService>>();
            var mockSessionProvider = new Mock<IUniviewSessionProvider>();
            var mockConfig = new Mock<IForensicsConfiguration>();
            var mockCredentialRepository = new Mock<ICredentialRepository>();

            // Create a real UniviewClient instance (won't connect unless methods are called)
            var client = new UniviewClient("192.168.1.1", "admin", "password");
            _ = mockSessionProvider.Setup(s => s.GetClient()).Returns(client);

            var service = new UniviewAuthService(
                mockLogger.Object,
                mockSessionProvider.Object,
                mockConfig.Object,
                mockCredentialRepository.Object);

            try
            {
                // Act
                string status = service.GetAuthStatus();

                // Assert
                Assert.Equal("Authenticated", status);
            }
            finally
            {
                client.Dispose();
            }
        }

        [Fact]
        public async Task UniviewAuthService_IsAuthenticatedAsync_NotAuthenticated_ReturnsFalse()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<UniviewAuthService>>();
            var mockSessionProvider = new Mock<IUniviewSessionProvider>();
            var mockConfig = new Mock<IForensicsConfiguration>();
            var mockCredentialRepository = new Mock<ICredentialRepository>();

            _ = mockSessionProvider.Setup(s => s.GetClient()).Returns((UniviewClient?)null);

            var service = new UniviewAuthService(
                mockLogger.Object,
                mockSessionProvider.Object,
                mockConfig.Object,
                mockCredentialRepository.Object);

            // Act
            bool result = await service.IsAuthenticatedAsync(CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task UniviewAuthService_RefreshAuthAsync_NotAuthenticated_ReturnsFalse()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<UniviewAuthService>>();
            var mockSessionProvider = new Mock<IUniviewSessionProvider>();
            var mockConfig = new Mock<IForensicsConfiguration>();
            var mockCredentialRepository = new Mock<ICredentialRepository>();

            _ = mockSessionProvider.Setup(s => s.GetClient()).Returns((UniviewClient?)null);

            var service = new UniviewAuthService(
                mockLogger.Object,
                mockSessionProvider.Object,
                mockConfig.Object,
                mockCredentialRepository.Object);

            // Act
            bool result = await service.RefreshAuthAsync(CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task UniviewAuthService_RestoreFromSavedCredentialsAsync_Parameterless_NoCredentials_ReturnsFalse()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<UniviewAuthService>>();
            var mockSessionProvider = new Mock<IUniviewSessionProvider>();
            var mockConfig = new Mock<IForensicsConfiguration>();
            var mockCredentialRepository = new Mock<ICredentialRepository>();

            // No credentials stored - GetAsync returns null
            _ = mockCredentialRepository
                .Setup(c => c.GetAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(null as (string CredentialType, string DecryptedValue)?);

            var service = new UniviewAuthService(
                mockLogger.Object,
                mockSessionProvider.Object,
                mockConfig.Object,
                mockCredentialRepository.Object);

            // Act
            bool result = await service.RestoreFromSavedCredentialsAsync(CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task UniviewAuthService_RestoreFromSavedCredentialsAsync_WithAccountId_NoCredentials_ReturnsFalse()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<UniviewAuthService>>();
            var mockSessionProvider = new Mock<IUniviewSessionProvider>();
            var mockConfig = new Mock<IForensicsConfiguration>();
            var mockCredentialRepository = new Mock<ICredentialRepository>();

            var accountId = Guid.NewGuid();

            // Credential repository returns null (no saved credentials)
            _ = mockCredentialRepository
                .Setup(c => c.GetAsync(accountId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(null as (string CredentialType, string DecryptedValue)?);

            var service = new UniviewAuthService(
                mockLogger.Object,
                mockSessionProvider.Object,
                mockConfig.Object,
                mockCredentialRepository.Object);

            // Act
            bool result = await service.RestoreFromSavedCredentialsAsync(accountId, CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void UniviewAuthService_Constructor_ThrowsOnNullLogger()
        {
            // Arrange
            var mockSessionProvider = new Mock<IUniviewSessionProvider>();
            var mockConfig = new Mock<IForensicsConfiguration>();
            var mockCredentialRepository = new Mock<ICredentialRepository>();

            // Act & Assert
            _ = Assert.Throws<ArgumentNullException>(() =>
                new UniviewAuthService(
                    null!,
                    mockSessionProvider.Object,
                    mockConfig.Object,
                    mockCredentialRepository.Object));
        }

        [Fact]
        public void UniviewAuthService_Constructor_ThrowsOnNullSessionProvider()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<UniviewAuthService>>();
            var mockConfig = new Mock<IForensicsConfiguration>();
            var mockCredentialRepository = new Mock<ICredentialRepository>();

            // Act & Assert
            _ = Assert.Throws<ArgumentNullException>(() =>
                new UniviewAuthService(
                    mockLogger.Object,
                    null!,
                    mockConfig.Object,
                    mockCredentialRepository.Object));
        }

        [Fact]
        public void UniviewAuthService_Constructor_ThrowsOnNullConfiguration()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<UniviewAuthService>>();
            var mockSessionProvider = new Mock<IUniviewSessionProvider>();
            var mockCredentialRepository = new Mock<ICredentialRepository>();

            // Act & Assert
            _ = Assert.Throws<ArgumentNullException>(() =>
                new UniviewAuthService(
                    mockLogger.Object,
                    mockSessionProvider.Object,
                    null!,
                    mockCredentialRepository.Object));
        }

        [Fact]
        public void UniviewAuthService_Constructor_ThrowsOnNullCredentialRepository()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<UniviewAuthService>>();
            var mockSessionProvider = new Mock<IUniviewSessionProvider>();
            var mockConfig = new Mock<IForensicsConfiguration>();

            // Act & Assert
            _ = Assert.Throws<ArgumentNullException>(() =>
                new UniviewAuthService(
                    mockLogger.Object,
                    mockSessionProvider.Object,
                    mockConfig.Object,
                    null!));
        }
    }
}
