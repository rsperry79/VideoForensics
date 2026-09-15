namespace VideoForensics.Forensics.Tests.KeyManagement
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using VideoForensics.Forensics.Exceptions;
    using VideoForensics.Forensics.KeyManagement;
    using Xunit;

    public class KeyStorageFactoryTests : IDisposable
    {
        private readonly string _tempDirectory;

        public KeyStorageFactoryTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDirectory);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        [Fact]
        public async Task GetDefaultProviderAsync_Always_ReturnsAvailableProvider()
        {
            // Act
            var provider = await KeyStorageFactory.GetDefaultProviderAsync();

            // Assert
            Assert.NotNull(provider);
            Assert.True(provider.IsAvailable);
        }

        [Fact]
        public async Task GetDefaultProviderAsync_Always_ReturnsFileBasedProvider()
        {
            // Act - Since TPM and DPAPI are not available on test machine,
            // the default should be file-based encrypted storage
            var provider = await KeyStorageFactory.GetDefaultProviderAsync();

            // Assert
            Assert.NotNull(provider);
            Assert.Equal("File-Based Encrypted Storage", provider.ProviderName);
            Assert.IsType<FileBasedKeyStorageProvider>(provider);
        }

        [Fact]
        public void CreateTpmProvider_Always_ReturnsTpmProvider()
        {
            // Act
            var provider = KeyStorageFactory.CreateTpmProvider();

            // Assert
            Assert.NotNull(provider);
            Assert.Equal("TPM 2.0", provider.ProviderName);
            Assert.IsType<TpmKeyStorageProvider>(provider);
        }

        [Fact]
        public void CreateTpmProvider_AlwaysReturnsFalseForIsAvailable()
        {
            // Act
            var provider = KeyStorageFactory.CreateTpmProvider();

            // Assert
            Assert.False(provider.IsAvailable);
        }

        [Fact]
        public void CreateDpapiProvider_Always_ReturnsDpapiProvider()
        {
            // Act
            var provider = KeyStorageFactory.CreateDpapiProvider();

            // Assert
            Assert.NotNull(provider);
            Assert.Equal("Windows DPAPI", provider.ProviderName);
            Assert.IsType<DpapiKeyStorageProvider>(provider);
        }

        [Fact]
        public void CreateDpapiProvider_WithStoragePath_IgnoresPath()
        {
            // Act - DPAPI provider doesn't use storage path but should accept it
            var provider = KeyStorageFactory.CreateDpapiProvider(_tempDirectory);

            // Assert
            Assert.NotNull(provider);
            Assert.Equal("Windows DPAPI", provider.ProviderName);
        }

        [Fact]
        public void CreateDpapiProvider_AlwaysReturnsFalseForIsAvailable()
        {
            // Act
            var provider = KeyStorageFactory.CreateDpapiProvider();

            // Assert
            Assert.False(provider.IsAvailable);
        }

        [Fact]
        public void CreateFileBasedProvider_WithValidPath_ReturnsFileBasedProvider()
        {
            // Act
            var provider = KeyStorageFactory.CreateFileBasedProvider(_tempDirectory);

            // Assert
            Assert.NotNull(provider);
            Assert.Equal("File-Based Encrypted Storage", provider.ProviderName);
            Assert.IsType<FileBasedKeyStorageProvider>(provider);
        }

        [Fact]
        public void CreateFileBasedProvider_Always_ReturnsAvailableProvider()
        {
            // Act
            var provider = KeyStorageFactory.CreateFileBasedProvider(_tempDirectory);

            // Assert
            Assert.True(provider.IsAvailable);
        }

        [Fact]
        public void CreateFileBasedProvider_WithDifferentPaths_CreatesIndependentProviders()
        {
            // Arrange
            string path1 = Path.Combine(_tempDirectory, "storage1");
            string path2 = Path.Combine(_tempDirectory, "storage2");
            Directory.CreateDirectory(path1);
            Directory.CreateDirectory(path2);

            // Act
            var provider1 = KeyStorageFactory.CreateFileBasedProvider(path1);
            var provider2 = KeyStorageFactory.CreateFileBasedProvider(path2);

            // Assert
            Assert.NotNull(provider1);
            Assert.NotNull(provider2);
            Assert.Equal("File-Based Encrypted Storage", provider1.ProviderName);
            Assert.Equal("File-Based Encrypted Storage", provider2.ProviderName);
        }

        [Fact]
        public async Task GetDefaultProviderAsync_CanStoreAndRetrieveKeys()
        {
            // Arrange
            var provider = await KeyStorageFactory.GetDefaultProviderAsync();
            string keyId = "factory-test-key";

            // Act
            string thumbprint = await provider.GenerateKeyPairAsync(keyId);
            var keys = await provider.ListKeysAsync();

            // Assert
            Assert.NotEmpty(thumbprint);
            Assert.Contains(keyId, keys);
        }

        [Fact]
        public async Task ProviderPriority_PrefersTpmIfAvailable()
        {
            // This test documents the expected behavior:
            // The factory tries TPM first, but since it's not available in test,
            // it falls back to the next available provider (File-Based)

            // Act
            var provider = await KeyStorageFactory.GetDefaultProviderAsync();

            // Assert
            // TPM is not available on most test machines, so we get File-Based
            Assert.True(provider.IsAvailable);
            Assert.NotEqual("TPM 2.0", provider.ProviderName);
        }
    }
}
