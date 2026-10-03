using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using VideoForensics.Forensics.Exceptions;
using VideoForensics.Forensics.KeyManagement;

namespace VideoForensics.Forensics.Tests.KeyManagement
{
    public class DpapiKeyStorageProviderTests : IDisposable
    {
        private readonly string _testKeyStorageDir;
        private readonly DpapiKeyStorageProvider _provider;

        public DpapiKeyStorageProviderTests()
        {
            // Use a temporary directory for test key storage
            _testKeyStorageDir = Path.Combine(Path.GetTempPath(), $"dpapi-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testKeyStorageDir);
            _provider = new DpapiKeyStorageProvider(_testKeyStorageDir);
        }

        public void Dispose()
        {
            // Clean up test directory
            if (Directory.Exists(_testKeyStorageDir))
            {
                Directory.Delete(_testKeyStorageDir, recursive: true);
            }
        }

        [Fact]
        public void ProviderName_Always_ReturnsExpectedName()
        {
            // Act & Assert
            Assert.Equal("Windows DPAPI", _provider.ProviderName);
        }

        [Fact(Skip = "Platform-specific: Windows only")]
        public void IsAvailable_Windows_ReturnsTrue()
        {
            // This test only runs on Windows; skip on other platforms
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                throw new InvalidOperationException("This test is Windows-only");
            }

            // Act & Assert
            Assert.True(_provider.IsAvailable);
        }

        [Fact]
        public async Task GenerateKeyPairAsync_WithValidKeyId_CreatesAndStoresKeyPair()
        {
            // Skip on non-Windows platforms
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            // Arrange
            string keyId = "test-key-gen-1";

            // Act
            string thumbprint = await _provider.GenerateKeyPairAsync(keyId);

            // Assert
            Assert.NotEmpty(thumbprint);
            Assert.Matches(@"^[0-9A-F]{40}$", thumbprint); // Thumbprint is 40 hex chars

            // Verify that public cert was exported
            string certPath = Path.Combine(_testKeyStorageDir, $"{keyId}.cer");
            Assert.True(File.Exists(certPath), $"Public cert file should exist at {certPath}");

            // Verify that private key file was created
            string privateKeyPath = Path.Combine(_testKeyStorageDir, $"{keyId}.private.key");
            Assert.True(File.Exists(privateKeyPath), $"Private key file should exist at {privateKeyPath}");

            // Verify private key content is not plaintext (should be encrypted via DPAPI)
            byte[] encryptedKeyBytes = File.ReadAllBytes(privateKeyPath);
            string keyAsString = Encoding.UTF8.GetString(encryptedKeyBytes);
            Assert.DoesNotContain("-----BEGIN", keyAsString); // Should not be PEM format
        }

        [Fact]
        public async Task GetPublicKeyAsync_WithExistingKey_ReturnsPublicKey()
        {
            // Skip on non-Windows platforms
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            // Arrange
            string keyId = "test-key-get-pub-1";
            await _provider.GenerateKeyPairAsync(keyId);

            // Act
            byte[] publicKeyBytes = await _provider.GetPublicKeyAsync(keyId);

            // Assert
            Assert.NotEmpty(publicKeyBytes);

            // Verify it's a valid X509 cert
#if NET10_0_OR_GREATER
            var cert = X509CertificateLoader.LoadCertificate(publicKeyBytes);
#else
            var cert = new X509Certificate2(publicKeyBytes);
#endif
            Assert.NotNull(cert);
            Assert.NotEmpty(cert.Thumbprint);
        }

        [Fact]
        public async Task SignDataAsync_WithValidKeyAndData_ReturnsValidSignature()
        {
            // Skip on non-Windows platforms
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            // Arrange
            string keyId = "test-key-sign-1";
            await _provider.GenerateKeyPairAsync(keyId);
            byte[] data = Encoding.UTF8.GetBytes("Test data for signing");

            // Act
            string signature = await _provider.SignDataAsync(keyId, data);

            // Assert
            Assert.NotEmpty(signature);

            // Verify signature is base64 encoded
            byte[] signatureBytes = Convert.FromBase64String(signature);
            Assert.NotEmpty(signatureBytes);
            Assert.True(signatureBytes.Length > 0, "Signature should not be empty");
        }

        [Fact]
        public async Task SignDataAsync_WithDifferentData_ReturnsDifferentSignatures()
        {
            // Skip on non-Windows platforms
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            // Arrange
            string keyId = "test-key-sign-diff-1";
            await _provider.GenerateKeyPairAsync(keyId);
            byte[] data1 = Encoding.UTF8.GetBytes("Data 1");
            byte[] data2 = Encoding.UTF8.GetBytes("Data 2");

            // Act
            string signature1 = await _provider.SignDataAsync(keyId, data1);
            string signature2 = await _provider.SignDataAsync(keyId, data2);

            // Assert
            Assert.NotEqual(signature1, signature2);
        }

        [Fact]
        public async Task VerifySignatureAsync_WithCorrectSignature_ReturnsTrue()
        {
            // Skip on non-Windows platforms
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            // Arrange
            string keyId = "test-key-verify-correct-1";
            await _provider.GenerateKeyPairAsync(keyId);
            byte[] data = Encoding.UTF8.GetBytes("Test data for verification");
            string signature = await _provider.SignDataAsync(keyId, data);

            // Act
            bool isValid = await _provider.VerifySignatureAsync(keyId, data, signature);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public async Task VerifySignatureAsync_WithIncorrectSignature_ReturnsFalse()
        {
            // Skip on non-Windows platforms
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            // Arrange
            string keyId = "test-key-verify-incorrect-1";
            await _provider.GenerateKeyPairAsync(keyId);
            byte[] data = Encoding.UTF8.GetBytes("Test data");
            byte[] wrongData = Encoding.UTF8.GetBytes("Wrong data");

            // Create a signature for the wrong data
            string wrongSignature = await _provider.SignDataAsync(keyId, wrongData);

            // Act
            bool isValid = await _provider.VerifySignatureAsync(keyId, data, wrongSignature);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public async Task DeleteKeyAsync_WithExistingKey_RemovesKeyFiles()
        {
            // Skip on non-Windows platforms
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            // Arrange
            string keyId = "test-key-delete-1";
            await _provider.GenerateKeyPairAsync(keyId);

            string certPath = Path.Combine(_testKeyStorageDir, $"{keyId}.cer");
            string privateKeyPath = Path.Combine(_testKeyStorageDir, $"{keyId}.private.key");

            // Verify files exist
            Assert.True(File.Exists(certPath));
            Assert.True(File.Exists(privateKeyPath));

            // Act
            await _provider.DeleteKeyAsync(keyId, "test-officer");

            // Assert
            Assert.False(File.Exists(certPath), "Public cert should be deleted");
            Assert.False(File.Exists(privateKeyPath), "Private key should be deleted");
        }

        [Fact]
        public async Task ListKeysAsync_AfterGeneratingKeys_ReturnsAllKeyIds()
        {
            // Skip on non-Windows platforms
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            // Arrange
            string keyId1 = "list-test-key-1";
            string keyId2 = "list-test-key-2";
            string keyId3 = "list-test-key-3";

            await _provider.GenerateKeyPairAsync(keyId1);
            await _provider.GenerateKeyPairAsync(keyId2);
            await _provider.GenerateKeyPairAsync(keyId3);

            // Act
            IEnumerable<string> keys = await _provider.ListKeysAsync();

            // Assert
            var keyList = keys.ToList();
            Assert.Contains(keyId1, keyList);
            Assert.Contains(keyId2, keyList);
            Assert.Contains(keyId3, keyList);
        }

        [Fact]
        public async Task GetKeyMetadataAsync_WithExistingKey_ReturnsValidMetadata()
        {
            // Skip on non-Windows platforms
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            // Arrange
            string keyId = "test-key-metadata-1";
            string thumbprint = await _provider.GenerateKeyPairAsync(keyId);

            // Act
            KeyMetadata metadata = await _provider.GetKeyMetadataAsync(keyId);

            // Assert
            Assert.NotNull(metadata);
            Assert.Equal(keyId, metadata.KeyId);
            Assert.Equal("Windows DPAPI", metadata.StorageProvider);
            Assert.Equal(thumbprint, metadata.CertificateThumbprint);
            Assert.Equal("RSA-2048", metadata.Algorithm);
            Assert.True(metadata.CreatedUtc > DateTime.UtcNow.AddSeconds(-10), "CreatedUtc should be recent");
            Assert.True(metadata.ExpiresUtc > DateTime.UtcNow, "ExpiresUtc should be in the future");
        }
    }
}
