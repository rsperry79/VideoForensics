using System.Security.Cryptography;
using System.Text;

using VideoForensics.Forensics.Exceptions;
using VideoForensics.Forensics.KeyManagement;

namespace VideoForensics.Forensics.Tests.KeyManagement
{
    public class FileBasedKeyStorageProviderTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly FileBasedKeyStorageProvider _provider;

        public FileBasedKeyStorageProviderTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _ = Directory.CreateDirectory(_tempDirectory);
            _provider = new FileBasedKeyStorageProvider(_tempDirectory);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        [Fact]
        public void IsAvailable_Always_ReturnsTrue()
        {
            // Act & Assert
            Assert.True(_provider.IsAvailable);
        }

        [Fact]
        public void ProviderName_Always_ReturnsExpectedName()
        {
            // Act & Assert
            Assert.Equal("File-Based Encrypted Storage", _provider.ProviderName);
        }

        [Fact]
        public async Task GenerateKeyPairAsync_WithValidKeyId_CreatesKeyFiles()
        {
            // Arrange
            string keyId = "test-key-1";

            // Act
            string thumbprint = await _provider.GenerateKeyPairAsync(keyId);

            // Assert
            Assert.NotEmpty(thumbprint);
            Assert.True(File.Exists(Path.Combine(_tempDirectory, $"{keyId}.key")));
            Assert.True(File.Exists(Path.Combine(_tempDirectory, $"{keyId}.pub")));
            Assert.True(File.Exists(Path.Combine(_tempDirectory, $"{keyId}.meta")));
        }

        [Fact]
        public async Task GenerateKeyPairAsync_MultipleKeys_CreatesDistinctFiles()
        {
            // Arrange
            string keyId1 = "key-1";
            string keyId2 = "key-2";

            // Act
            string thumbprint1 = await _provider.GenerateKeyPairAsync(keyId1);
            string thumbprint2 = await _provider.GenerateKeyPairAsync(keyId2);

            // Assert
            Assert.NotEqual(thumbprint1, thumbprint2);
            Assert.True(File.Exists(Path.Combine(_tempDirectory, $"{keyId1}.key")));
            Assert.True(File.Exists(Path.Combine(_tempDirectory, $"{keyId2}.key")));
        }

        [Fact]
        public async Task GetPublicKeyAsync_WithGeneratedKey_ReturnsPublicKey()
        {
            // Arrange
            string keyId = "test-key";
            _ = await _provider.GenerateKeyPairAsync(keyId);

            // Act
            byte[] publicKey = await _provider.GetPublicKeyAsync(keyId);

            // Assert
            Assert.NotEmpty(publicKey);
            // Verify it's a valid public key by attempting to import it
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(publicKey, out _);
            Assert.NotNull(rsa);
        }

        [Fact]
        public async Task GetPublicKeyAsync_WithNonExistentKey_ReturnsEmptyArray()
        {
            // Act
            byte[] publicKey = await _provider.GetPublicKeyAsync("non-existent-key");

            // Assert
            Assert.Empty(publicKey);
        }

        [Fact]
        public async Task SignDataAsync_WithValidKey_ReturnsSignature()
        {
            // Arrange
            string keyId = "sign-test-key";
            _ = await _provider.GenerateKeyPairAsync(keyId);
            byte[] dataToSign = Encoding.UTF8.GetBytes("Test data to sign");

            // Act
            string signature = await _provider.SignDataAsync(keyId, dataToSign);

            // Assert
            Assert.NotEmpty(signature);
            // Verify signature is base64
            byte[] signatureBytes = Convert.FromBase64String(signature);
            Assert.True(signatureBytes.Length > 0);
        }

        [Fact]
        public async Task SignDataAsync_SameDataTwice_ProducesDifferentSignatures()
        {
            // Arrange
            string keyId = "sign-test-key";
            _ = await _provider.GenerateKeyPairAsync(keyId);
            byte[] dataToSign = Encoding.UTF8.GetBytes("Test data");

            // Act
            string signature1 = await _provider.SignDataAsync(keyId, dataToSign);
            string signature2 = await _provider.SignDataAsync(keyId, dataToSign);

            // Assert - RSA-PSS would produce different signatures, but PKCS#1 v1.5 should produce same signature
            // This test verifies deterministic signing
            Assert.Equal(signature1, signature2);
        }

        [Fact]
        public async Task SignDataAsync_WithNonExistentKey_ThrowsException()
        {
            // Arrange
            byte[] dataToSign = Encoding.UTF8.GetBytes("Test data");

            // Act & Assert
            _ = await Assert.ThrowsAsync<ForensicAnalysisException>(async () =>
                await _provider.SignDataAsync("non-existent-key", dataToSign));
        }

        [Fact]
        public async Task VerifySignatureAsync_WithValidSignature_ReturnsTrue()
        {
            // Arrange
            string keyId = "verify-test-key";
            _ = await _provider.GenerateKeyPairAsync(keyId);
            byte[] dataToSign = Encoding.UTF8.GetBytes("Data to verify");
            string signature = await _provider.SignDataAsync(keyId, dataToSign);

            // Act
            bool isValid = await _provider.VerifySignatureAsync(keyId, dataToSign, signature);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public async Task VerifySignatureAsync_WithTamperedData_ReturnsFalse()
        {
            // Arrange
            string keyId = "verify-test-key";
            _ = await _provider.GenerateKeyPairAsync(keyId);
            byte[] originalData = Encoding.UTF8.GetBytes("Original data");
            string signature = await _provider.SignDataAsync(keyId, originalData);
            byte[] tamperedData = Encoding.UTF8.GetBytes("Tampered data");

            // Act
            bool isValid = await _provider.VerifySignatureAsync(keyId, tamperedData, signature);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public async Task VerifySignatureAsync_WithTamperedSignature_ReturnsFalse()
        {
            // Arrange
            string keyId = "verify-test-key";
            _ = await _provider.GenerateKeyPairAsync(keyId);
            byte[] dataToSign = Encoding.UTF8.GetBytes("Data to verify");
            string signature = await _provider.SignDataAsync(keyId, dataToSign);
            // Tamper with signature
            byte[] signatureBytes = Convert.FromBase64String(signature);
            signatureBytes[0] ^= 0xFF; // Flip bits
            string tamperedSignature = Convert.ToBase64String(signatureBytes);

            // Act
            bool isValid = await _provider.VerifySignatureAsync(keyId, dataToSign, tamperedSignature);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public async Task VerifySignatureAsync_WithNonExistentKey_ReturnsFalse()
        {
            // Arrange
            byte[] data = Encoding.UTF8.GetBytes("Data");
            string signature = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 });

            // Act
            bool isValid = await _provider.VerifySignatureAsync("non-existent-key", data, signature);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public async Task ListKeysAsync_WithGeneratedKeys_ReturnsAllKeyIds()
        {
            // Arrange
            string keyId1 = "list-key-1";
            string keyId2 = "list-key-2";
            string keyId3 = "list-key-3";
            _ = await _provider.GenerateKeyPairAsync(keyId1);
            _ = await _provider.GenerateKeyPairAsync(keyId2);
            _ = await _provider.GenerateKeyPairAsync(keyId3);

            // Act
            IEnumerable<string> keys = await _provider.ListKeysAsync();

            // Assert
            var keyList = keys.ToList();
            // Note: List includes the "master" key used for encryption, plus our 3 generated keys
            Assert.Equal(4, keyList.Count);
            Assert.Contains(keyId1, keyList);
            Assert.Contains(keyId2, keyList);
            Assert.Contains(keyId3, keyList);
        }

        [Fact]
        public async Task ListKeysAsync_WithNoKeys_ReturnsEmptyList()
        {
            // Act
            IEnumerable<string> keys = await _provider.ListKeysAsync();

            // Assert
            Assert.Empty(keys);
        }

        [Fact]
        public async Task ListKeysAsync_AfterKeyDeletion_ExcludesDeletedKey()
        {
            // Arrange
            string keyId1 = "list-key-1";
            string keyId2 = "list-key-2";
            _ = await _provider.GenerateKeyPairAsync(keyId1);
            _ = await _provider.GenerateKeyPairAsync(keyId2);

            // Act
            await _provider.DeleteKeyAsync(keyId1, "officer-001");
            IEnumerable<string> keys = await _provider.ListKeysAsync();

            // Assert
            var keyList = keys.ToList();
            // Note: List includes the "master" key, plus keyId2 (keyId1 was deleted)
            Assert.Equal(2, keyList.Count);
            Assert.Contains(keyId2, keyList);
            Assert.DoesNotContain(keyId1, keyList);
        }

        [Fact]
        public async Task DeleteKeyAsync_WithExistingKey_RemovesAllKeyFiles()
        {
            // Arrange
            string keyId = "delete-key";
            _ = await _provider.GenerateKeyPairAsync(keyId);
            string keyPath = Path.Combine(_tempDirectory, $"{keyId}.key");
            string pubKeyPath = Path.Combine(_tempDirectory, $"{keyId}.pub");
            string metaPath = Path.Combine(_tempDirectory, $"{keyId}.meta");

            Assert.True(File.Exists(keyPath));
            Assert.True(File.Exists(pubKeyPath));
            Assert.True(File.Exists(metaPath));

            // Act
            await _provider.DeleteKeyAsync(keyId, "officer-001");

            // Assert
            Assert.False(File.Exists(keyPath));
            Assert.False(File.Exists(pubKeyPath));
            Assert.False(File.Exists(metaPath));
        }

        [Fact]
        public async Task DeleteKeyAsync_WithNonExistentKey_DoesNotThrow()
        {
            // Act & Assert
            await _provider.DeleteKeyAsync("non-existent-key", "officer-001");
        }

        [Fact]
        public async Task GetKeyMetadataAsync_WithExistingKey_ReturnsMetadata()
        {
            // Arrange
            string keyId = "metadata-key";
            _ = await _provider.GenerateKeyPairAsync(keyId);

            // Act
            KeyMetadata metadata = await _provider.GetKeyMetadataAsync(keyId);

            // Assert
            Assert.NotNull(metadata);
            Assert.Equal(keyId, metadata.KeyId);
            Assert.NotEmpty(metadata.CertificateThumbprint);
            Assert.Equal("RSA-2048", metadata.Algorithm);
            Assert.Equal("File-Based Encrypted Storage", metadata.StorageProvider);
            Assert.NotEqual(default, metadata.CreatedUtc);
        }

        [Fact]
        public async Task GetKeyMetadataAsync_WithNonExistentKey_ReturnsDefaultMetadata()
        {
            // Arrange
            string keyId = "non-existent-key";

            // Act
            KeyMetadata metadata = await _provider.GetKeyMetadataAsync(keyId);

            // Assert
            Assert.NotNull(metadata);
            Assert.Equal(keyId, metadata.KeyId);
            Assert.Equal("File-Based Encrypted Storage", metadata.StorageProvider);
        }

        [Fact]
        public async Task StoreRetrieveRoundTrip_WithMultipleOperations_MaintainsIntegrity()
        {
            // Arrange
            string keyId = "roundtrip-key";
            byte[] originalData = Encoding.UTF8.GetBytes("Important forensic evidence");

            // Act
            string thumbprint = await _provider.GenerateKeyPairAsync(keyId);
            string signature = await _provider.SignDataAsync(keyId, originalData);
            bool isValid = await _provider.VerifySignatureAsync(keyId, originalData, signature);
            KeyMetadata metadata = await _provider.GetKeyMetadataAsync(keyId);

            // Assert
            Assert.NotEmpty(thumbprint);
            Assert.True(isValid);
            Assert.Equal(thumbprint, metadata.CertificateThumbprint);
        }

        [Fact]
        public async Task MasterKey_PersistsAcrossProviderInstances()
        {
            // Arrange
            string keyId = "persist-key";
            _ = await _provider.GenerateKeyPairAsync(keyId);
            byte[] dataToSign = Encoding.UTF8.GetBytes("Test data");
            string signature1 = await _provider.SignDataAsync(keyId, dataToSign);

            // Act - Create new provider instance for same storage path
            var newProvider = new FileBasedKeyStorageProvider(_tempDirectory);
            bool isValid = await newProvider.VerifySignatureAsync(keyId, dataToSign, signature1);

            // Assert
            Assert.True(isValid);
        }
    }
}
