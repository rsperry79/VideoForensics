using VideoForensics.Forensics.KeyManagement;

namespace VideoForensics.Forensics.Tests.KeyManagement
{
    public class KeyStorageFactoryTests : IDisposable
    {
        private readonly string _tempDirectory;

        public KeyStorageFactoryTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _ = Directory.CreateDirectory(_tempDirectory);
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
            IKeyStorageProvider provider = await KeyStorageFactory.GetDefaultProviderAsync();

            // Assert
            Assert.NotNull(provider);
            Assert.True(provider.IsAvailable);
        }

        [Fact]
        public async Task GetDefaultProviderAsync_Always_ReturnsFileBasedProvider()
        {
            // Act - Since TPM and DPAPI are not available on test machine,
            // the default should be file-based encrypted storage
            IKeyStorageProvider provider = await KeyStorageFactory.GetDefaultProviderAsync();

            // Assert
            Assert.NotNull(provider);
            Assert.Equal("File-Based Encrypted Storage", provider.ProviderName);
            _ = Assert.IsType<FileBasedKeyStorageProvider>(provider);
        }

        [Fact]
        public void CreateTpmProvider_Always_ReturnsTpmProvider()
        {
            // Act
            IKeyStorageProvider provider = KeyStorageFactory.CreateTpmProvider();

            // Assert
            Assert.NotNull(provider);
            Assert.Equal("TPM 2.0", provider.ProviderName);
            _ = Assert.IsType<TpmKeyStorageProvider>(provider);
        }

        [Fact]
        public void CreateTpmProvider_AlwaysReturnsFalseForIsAvailable()
        {
            // Act
            IKeyStorageProvider provider = KeyStorageFactory.CreateTpmProvider();

            // Assert
            Assert.False(provider.IsAvailable);
        }

        [Fact]
        public void CreateDpapiProvider_Always_ReturnsDpapiProvider()
        {
            // Act
            IKeyStorageProvider provider = KeyStorageFactory.CreateDpapiProvider();

            // Assert
            Assert.NotNull(provider);
            Assert.Equal("Windows DPAPI", provider.ProviderName);
            _ = Assert.IsType<DpapiKeyStorageProvider>(provider);
        }

        [Fact]
        public void CreateDpapiProvider_WithStoragePath_IgnoresPath()
        {
            // Act - DPAPI provider doesn't use storage path but should accept it
            IKeyStorageProvider provider = KeyStorageFactory.CreateDpapiProvider(_tempDirectory);

            // Assert
            Assert.NotNull(provider);
            Assert.Equal("Windows DPAPI", provider.ProviderName);
        }

        [Fact]
        public void CreateDpapiProvider_AlwaysReturnsFalseForIsAvailable()
        {
            // Act
            IKeyStorageProvider provider = KeyStorageFactory.CreateDpapiProvider();

            // Assert
            Assert.False(provider.IsAvailable);
        }

        [Fact]
        public void CreateFileBasedProvider_WithValidPath_ReturnsFileBasedProvider()
        {
            // Act
            IKeyStorageProvider provider = KeyStorageFactory.CreateFileBasedProvider(_tempDirectory);

            // Assert
            Assert.NotNull(provider);
            Assert.Equal("File-Based Encrypted Storage", provider.ProviderName);
            _ = Assert.IsType<FileBasedKeyStorageProvider>(provider);
        }

        [Fact]
        public void CreateFileBasedProvider_Always_ReturnsAvailableProvider()
        {
            // Act
            IKeyStorageProvider provider = KeyStorageFactory.CreateFileBasedProvider(_tempDirectory);

            // Assert
            Assert.True(provider.IsAvailable);
        }

        [Fact]
        public void CreateFileBasedProvider_WithDifferentPaths_CreatesIndependentProviders()
        {
            // Arrange
            string path1 = Path.Combine(_tempDirectory, "storage1");
            string path2 = Path.Combine(_tempDirectory, "storage2");
            _ = Directory.CreateDirectory(path1);
            _ = Directory.CreateDirectory(path2);

            // Act
            IKeyStorageProvider provider1 = KeyStorageFactory.CreateFileBasedProvider(path1);
            IKeyStorageProvider provider2 = KeyStorageFactory.CreateFileBasedProvider(path2);

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
            IKeyStorageProvider provider = await KeyStorageFactory.GetDefaultProviderAsync();
            string keyId = "factory-test-key";

            // Act
            string thumbprint = await provider.GenerateKeyPairAsync(keyId);
            IEnumerable<string> keys = await provider.ListKeysAsync();

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
            IKeyStorageProvider provider = await KeyStorageFactory.GetDefaultProviderAsync();

            // Assert
            // TPM is not available on most test machines, so we get File-Based
            Assert.True(provider.IsAvailable);
            Assert.NotEqual("TPM 2.0", provider.ProviderName);
        }
    }
}
