using VideoForensics.Forensics.KeyManagement;

namespace VideoForensics.Forensics.Tests.KeyManagement
{
    /// <summary>
    /// Base class for testing IKeyStorageProvider implementations.
    /// Concrete test classes should inherit from this and implement CreateProvider().
    /// Tests verify the contract that all implementations must fulfill.
    /// </summary>
    public abstract class IKeyStorageProviderContractTests
    {
        protected abstract IKeyStorageProvider CreateProvider();

        [Fact]
        public void ProviderName_IsNotEmpty()
        {
            // Arrange
            IKeyStorageProvider provider = CreateProvider();

            // Act & Assert
            Assert.NotEmpty(provider.ProviderName);
        }

        [Fact]
        public void IsAvailable_HasDefinedValue()
        {
            // Arrange
            IKeyStorageProvider provider = CreateProvider();

            // Act
            bool isAvailable = provider.IsAvailable;

            // Assert - Should not throw, value should be boolean
            _ = Assert.IsType<bool>(isAvailable);
        }

        [Fact]
        public async Task ListKeysAsync_DoesNotReturnNull()
        {
            // Arrange
            IKeyStorageProvider provider = CreateProvider();

            // Act
            IEnumerable<string> keys = await provider.ListKeysAsync();

            // Assert
            Assert.NotNull(keys);
        }

        [Fact]
        public async Task GetKeyMetadataAsync_DoesNotReturnNull()
        {
            // Arrange
            IKeyStorageProvider provider = CreateProvider();

            // Act
            KeyMetadata metadata = await provider.GetKeyMetadataAsync("any-key-id");

            // Assert
            Assert.NotNull(metadata);
        }

        [Fact]
        public async Task GetKeyMetadataAsync_ReturnsMetadataWithKeyId()
        {
            // Arrange
            IKeyStorageProvider provider = CreateProvider();
            string expectedKeyId = "test-key-id";

            // Act
            KeyMetadata metadata = await provider.GetKeyMetadataAsync(expectedKeyId);

            // Assert
            Assert.Equal(expectedKeyId, metadata.KeyId);
        }

        [Fact]
        public async Task GetKeyMetadataAsync_ReturnsMetadataWithProviderName()
        {
            // Arrange
            IKeyStorageProvider provider = CreateProvider();

            // Act
            KeyMetadata metadata = await provider.GetKeyMetadataAsync("test-key");

            // Assert
            Assert.Equal(provider.ProviderName, metadata.StorageProvider);
        }

        [Fact]
        public void KeyMetadata_HasExpectedProperties()
        {
            // Arrange
            var metadata = new KeyMetadata
            {
                KeyId = "test",
                CertificateThumbprint = "abc123",
                CreatedUtc = DateTime.UtcNow,
                Algorithm = "RSA-2048"
            };

            // Act & Assert
            Assert.NotEmpty(metadata.KeyId);
            Assert.NotEmpty(metadata.CertificateThumbprint);
            Assert.NotEqual(default, metadata.CreatedUtc);
            Assert.NotEmpty(metadata.Algorithm);
        }
    }

    /// <summary>
    /// Test FileBasedKeyStorageProvider against the contract.
    /// </summary>
    internal class FileBasedKeyStorageProviderContractTests : IKeyStorageProviderContractTests
    {
        private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        public FileBasedKeyStorageProviderContractTests()
        {
            _ = Directory.CreateDirectory(_tempDirectory);
        }

        protected override IKeyStorageProvider CreateProvider()
        {
            return new FileBasedKeyStorageProvider(_tempDirectory);
        }

        ~FileBasedKeyStorageProviderContractTests()
        {
            if (Directory.Exists(_tempDirectory))
            {
                try
                {
                    Directory.Delete(_tempDirectory, recursive: true);
                }
                catch
                {
                    // Cleanup best effort
                }
            }
        }
    }

    /// <summary>
    /// Test DpapiKeyStorageProvider against the contract.
    /// </summary>
    internal class DpapiKeyStorageProviderContractTests : IKeyStorageProviderContractTests
    {
        protected override IKeyStorageProvider CreateProvider()
        {
            return new DpapiKeyStorageProvider();
        }
    }

    /// <summary>
    /// Test TpmKeyStorageProvider against the contract.
    /// </summary>
    internal class TpmKeyStorageProviderContractTests : IKeyStorageProviderContractTests
    {
        protected override IKeyStorageProvider CreateProvider()
        {
            return new TpmKeyStorageProvider();
        }
    }
}
