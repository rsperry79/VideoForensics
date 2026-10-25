using Moq;

using VideoForensics.Core.Helpers.Forensics.KeyManagement;

namespace VideoForensics.Core.Helpers.Forensics.Tests.KeyManagement
{
    public class KeyStorageFactoryTests : IDisposable
    {
        private readonly string _tempDirectory;

        public KeyStorageFactoryTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _ = Directory.CreateDirectory(_tempDirectory);
            // Core has no platform providers of its own; start each test with none registered.
            KeyStorageProviders.PlatformProviders = null;
        }

        public void Dispose()
        {
            KeyStorageProviders.PlatformProviders = null;
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

        /// <summary>Verifies that with no platform providers registered, Core falls back to file-based storage on every OS.</summary>
        [Fact]
        public async Task GetDefaultProviderAsync_NoPlatformProviders_ReturnsFileBased()
        {
            IKeyStorageProvider provider = await KeyStorageFactory.GetDefaultProviderAsync();

            Assert.Equal("File-Based Encrypted Storage", provider.ProviderName);
            _ = Assert.IsType<FileBasedKeyStorageProvider>(provider);
        }

        /// <summary>Verifies that a registered platform provider that is available is chosen over the file-based fallback.</summary>
        [Fact]
        public async Task GetDefaultProviderAsync_AvailablePlatformProvider_IsChosenFirst()
        {
            var platform = new Mock<IKeyStorageProvider>();
            _ = platform.Setup(p => p.IsAvailable).Returns(true);
            _ = platform.Setup(p => p.ProviderName).Returns("Platform Test");
            KeyStorageProviders.PlatformProviders = () => new[] { platform.Object };

            IKeyStorageProvider provider = await KeyStorageFactory.GetDefaultProviderAsync();

            Assert.Same(platform.Object, provider);
        }

        /// <summary>Verifies that an unavailable platform provider is skipped, so the file-based fallback is used.</summary>
        [Fact]
        public async Task GetDefaultProviderAsync_UnavailablePlatformProvider_IsSkipped()
        {
            var platform = new Mock<IKeyStorageProvider>();
            _ = platform.Setup(p => p.IsAvailable).Returns(false);
            KeyStorageProviders.PlatformProviders = () => new[] { platform.Object };

            IKeyStorageProvider provider = await KeyStorageFactory.GetDefaultProviderAsync();

            _ = Assert.IsType<FileBasedKeyStorageProvider>(provider);
        }

        /// <summary>Verifies that a platform source that throws leaves the file-based fallback in place rather than failing the factory.</summary>
        [Fact]
        public async Task GetDefaultProviderAsync_PlatformSourceThrows_FallsBackToFileBased()
        {
            KeyStorageProviders.PlatformProviders = () => throw new InvalidOperationException("no TPM");

            IKeyStorageProvider provider = await KeyStorageFactory.GetDefaultProviderAsync();

            _ = Assert.IsType<FileBasedKeyStorageProvider>(provider);
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
    }
}
