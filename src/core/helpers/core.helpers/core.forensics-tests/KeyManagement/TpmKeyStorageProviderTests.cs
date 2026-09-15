using System.Text;

using VideoForensics.Forensics.Exceptions;
using VideoForensics.Forensics.KeyManagement;

namespace VideoForensics.Forensics.Tests.KeyManagement
{
    public class TpmKeyStorageProviderTests
    {
        private readonly TpmKeyStorageProvider _provider = new();

        [Fact]
        public void IsAvailable_Always_ReturnsFalse()
        {
            // Act & Assert
            Assert.False(_provider.IsAvailable);
        }

        [Fact]
        public void ProviderName_Always_ReturnsExpectedName()
        {
            // Act & Assert
            Assert.Equal("TPM 2.0", _provider.ProviderName);
        }

        [Fact]
        public async Task GenerateKeyPairAsync_Always_ThrowsForensicAnalysisException()
        {
            // Act & Assert
            _ = await Assert.ThrowsAsync<ForensicAnalysisException>(async () =>
                await _provider.GenerateKeyPairAsync("test-key"));
        }

        [Fact]
        public async Task GetPublicKeyAsync_Always_ThrowsForensicAnalysisException()
        {
            // Act & Assert
            _ = await Assert.ThrowsAsync<ForensicAnalysisException>(async () =>
                await _provider.GetPublicKeyAsync("test-key"));
        }

        [Fact]
        public async Task SignDataAsync_Always_ThrowsForensicAnalysisException()
        {
            // Arrange
            byte[] data = Encoding.UTF8.GetBytes("Test data");

            // Act & Assert
            _ = await Assert.ThrowsAsync<ForensicAnalysisException>(async () =>
                await _provider.SignDataAsync("test-key", data));
        }

        [Fact]
        public async Task VerifySignatureAsync_Always_ThrowsForensicAnalysisException()
        {
            // Arrange
            byte[] data = Encoding.UTF8.GetBytes("Test data");
            string signature = "test-signature";

            // Act & Assert
            _ = await Assert.ThrowsAsync<ForensicAnalysisException>(async () =>
                await _provider.VerifySignatureAsync("test-key", data, signature));
        }

        [Fact]
        public async Task DeleteKeyAsync_Always_ThrowsForensicAnalysisException()
        {
            // Act & Assert
            _ = await Assert.ThrowsAsync<ForensicAnalysisException>(async () =>
                await _provider.DeleteKeyAsync("test-key", "officer-001"));
        }

        [Fact]
        public async Task ListKeysAsync_Always_ReturnsEmptyList()
        {
            // Act
            IEnumerable<string> keys = await _provider.ListKeysAsync();

            // Assert
            Assert.Empty(keys);
        }

        [Fact]
        public async Task GetKeyMetadataAsync_WithAnyKeyId_ReturnsBasicMetadata()
        {
            // Arrange
            string keyId = "test-key";

            // Act
            KeyMetadata metadata = await _provider.GetKeyMetadataAsync(keyId);

            // Assert
            Assert.NotNull(metadata);
            Assert.Equal(keyId, metadata.KeyId);
            Assert.Equal("TPM 2.0", metadata.StorageProvider);
        }

        [Fact]
        public async Task ExceptionMessages_AreInformative()
        {
            // Act & Assert
            ForensicAnalysisException exception = await Assert.ThrowsAsync<ForensicAnalysisException>(async () =>
                await _provider.GenerateKeyPairAsync("test-key"));

            Assert.Contains("TPM", exception.Message);
            Assert.Contains("not currently available", exception.Message);
        }

        [Fact]
        public async Task ExceptionMessages_MentionFallback_ForGenerateKeyPair()
        {
            // Act & Assert
            ForensicAnalysisException exception = await Assert.ThrowsAsync<ForensicAnalysisException>(async () =>
                await _provider.GenerateKeyPairAsync("test-key"));

            Assert.Contains("Falling back", exception.Message);
            Assert.Contains("DPAPI", exception.Message);
        }
    }
}
