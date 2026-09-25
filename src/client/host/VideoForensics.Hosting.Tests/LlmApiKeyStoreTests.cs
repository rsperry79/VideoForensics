using Moq;
using VideoForensics.Data.Common.Contracts;
using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class LlmApiKeyStoreTests
    {
        private const string EncryptedApiKeySettingKey = "Llm.EncryptedApiKey";
        private const string ProviderSettingKey = "Llm.Provider";
        private const string ModelSettingKey = "Llm.Model";
        private const string BaseUrlSettingKey = "Llm.BaseUrl";

        private static (Mock<IAppSettingRepository>, Mock<ICredentialEncryptionProvider>) CreateMocks()
        {
            var settingsMock = new Mock<IAppSettingRepository>();
            var encryptionMock = new Mock<ICredentialEncryptionProvider>();
            return (settingsMock, encryptionMock);
        }

        [Fact]
        public async Task SetApiKeyAsync_EncryptsBeforeStoring()
        {
            // Arrange
            var (settingsMock, encryptionMock) = CreateMocks();
            var plainApiKey = "sk-test-key-12345";
            var encryptedKey = "encrypted_sk-test-key-12345";

            encryptionMock
                .Setup(x => x.EncryptAsync(plainApiKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(encryptedKey);

            var store = new LlmApiKeyStore(settingsMock.Object, encryptionMock.Object);

            // Act
            await store.SetApiKeyAsync(plainApiKey, CancellationToken.None);

            // Assert
            encryptionMock.Verify(
                x => x.EncryptAsync(plainApiKey, It.IsAny<CancellationToken>()),
                Times.Once,
                "Encryption should be called exactly once");

            settingsMock.Verify(
                x => x.SetAsync(EncryptedApiKeySettingKey, encryptedKey, It.IsAny<CancellationToken>()),
                Times.Once,
                "Encrypted key should be stored with correct setting key");
        }

        [Fact]
        public async Task GetDecryptedApiKeyAsync_DecryptsOnRead()
        {
            // Arrange
            var (settingsMock, encryptionMock) = CreateMocks();
            var plainApiKey = "sk-test-key-12345";
            var encryptedKey = "encrypted_sk-test-key-12345";

            settingsMock
                .Setup(x => x.GetAsync(EncryptedApiKeySettingKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(encryptedKey);

            encryptionMock
                .Setup(x => x.DecryptAsync(encryptedKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(plainApiKey);

            var store = new LlmApiKeyStore(settingsMock.Object, encryptionMock.Object);

            // Act
            var result = await store.GetDecryptedApiKeyAsync(CancellationToken.None);

            // Assert
            Assert.Equal(plainApiKey, result);
            encryptionMock.Verify(
                x => x.DecryptAsync(encryptedKey, It.IsAny<CancellationToken>()),
                Times.Once,
                "Decryption should be called once");
        }

        [Fact]
        public async Task GetDecryptedApiKeyAsync_ReturnsNullWhenNotStored()
        {
            // Arrange
            var (settingsMock, encryptionMock) = CreateMocks();

            settingsMock
                .Setup(x => x.GetAsync(EncryptedApiKeySettingKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);

            var store = new LlmApiKeyStore(settingsMock.Object, encryptionMock.Object);

            // Act
            var result = await store.GetDecryptedApiKeyAsync(CancellationToken.None);

            // Assert
            Assert.Null(result);
            encryptionMock.Verify(
                x => x.DecryptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never,
                "Decryption should not be called when setting is null");
        }

        [Fact]
        public async Task GetDecryptedApiKeyAsync_ReturnsNullWhenEmptyString()
        {
            // Arrange
            var (settingsMock, encryptionMock) = CreateMocks();

            settingsMock
                .Setup(x => x.GetAsync(EncryptedApiKeySettingKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(string.Empty);

            var store = new LlmApiKeyStore(settingsMock.Object, encryptionMock.Object);

            // Act
            var result = await store.GetDecryptedApiKeyAsync(CancellationToken.None);

            // Assert
            Assert.Null(result);
            encryptionMock.Verify(
                x => x.DecryptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never,
                "Decryption should not be called when setting is empty");
        }

        [Fact]
        public async Task ClearApiKeyAsync_DeletesEncryptedKeySetting()
        {
            // Arrange
            var (settingsMock, encryptionMock) = CreateMocks();
            var store = new LlmApiKeyStore(settingsMock.Object, encryptionMock.Object);

            // Act
            await store.ClearApiKeyAsync(CancellationToken.None);

            // Assert
            settingsMock.Verify(
                x => x.DeleteAsync(EncryptedApiKeySettingKey, It.IsAny<CancellationToken>()),
                Times.Once,
                "Encrypted API key setting should be deleted");
        }

        [Fact]
        public async Task SetProviderAsync_StoresProviderName()
        {
            // Arrange
            var (settingsMock, encryptionMock) = CreateMocks();
            var provider = "Anthropic";
            var store = new LlmApiKeyStore(settingsMock.Object, encryptionMock.Object);

            // Act
            await store.SetProviderAsync(provider, CancellationToken.None);

            // Assert
            settingsMock.Verify(
                x => x.SetAsync(ProviderSettingKey, provider, It.IsAny<CancellationToken>()),
                Times.Once,
                "Provider should be stored with correct setting key");
        }

        [Fact]
        public async Task GetProviderAsync_ReturnsStoredProvider()
        {
            // Arrange
            var (settingsMock, encryptionMock) = CreateMocks();
            var provider = "Anthropic";

            settingsMock
                .Setup(x => x.GetAsync(ProviderSettingKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(provider);

            var store = new LlmApiKeyStore(settingsMock.Object, encryptionMock.Object);

            // Act
            var result = await store.GetProviderAsync(CancellationToken.None);

            // Assert
            Assert.Equal(provider, result);
        }

        [Fact]
        public async Task GetProviderAsync_ReturnsNullWhenNotSet()
        {
            // Arrange
            var (settingsMock, encryptionMock) = CreateMocks();

            settingsMock
                .Setup(x => x.GetAsync(ProviderSettingKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);

            var store = new LlmApiKeyStore(settingsMock.Object, encryptionMock.Object);

            // Act
            var result = await store.GetProviderAsync(CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task SetModelAsync_StoresModelName()
        {
            // Arrange
            var (settingsMock, encryptionMock) = CreateMocks();
            var model = "claude-3-sonnet-20240229";
            var store = new LlmApiKeyStore(settingsMock.Object, encryptionMock.Object);

            // Act
            await store.SetModelAsync(model, CancellationToken.None);

            // Assert
            settingsMock.Verify(
                x => x.SetAsync(ModelSettingKey, model, It.IsAny<CancellationToken>()),
                Times.Once,
                "Model should be stored with correct setting key");
        }

        [Fact]
        public async Task GetModelAsync_ReturnsStoredModel()
        {
            // Arrange
            var (settingsMock, encryptionMock) = CreateMocks();
            var model = "claude-3-sonnet-20240229";

            settingsMock
                .Setup(x => x.GetAsync(ModelSettingKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(model);

            var store = new LlmApiKeyStore(settingsMock.Object, encryptionMock.Object);

            // Act
            var result = await store.GetModelAsync(CancellationToken.None);

            // Assert
            Assert.Equal(model, result);
        }

        [Fact]
        public async Task GetModelAsync_ReturnsNullWhenNotSet()
        {
            // Arrange
            var (settingsMock, encryptionMock) = CreateMocks();

            settingsMock
                .Setup(x => x.GetAsync(ModelSettingKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);

            var store = new LlmApiKeyStore(settingsMock.Object, encryptionMock.Object);

            // Act
            var result = await store.GetModelAsync(CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task SetBaseUrlAsync_StoresBaseUrl()
        {
            // Arrange
            var (settingsMock, encryptionMock) = CreateMocks();
            var baseUrl = "https://api.openai.com/v1";
            var store = new LlmApiKeyStore(settingsMock.Object, encryptionMock.Object);

            // Act
            await store.SetBaseUrlAsync(baseUrl, CancellationToken.None);

            // Assert
            settingsMock.Verify(
                x => x.SetAsync(BaseUrlSettingKey, baseUrl, It.IsAny<CancellationToken>()),
                Times.Once,
                "Base URL should be stored with correct setting key");
        }

        [Fact]
        public async Task GetBaseUrlAsync_ReturnsStoredBaseUrl()
        {
            // Arrange
            var (settingsMock, encryptionMock) = CreateMocks();
            var baseUrl = "https://api.openai.com/v1";

            settingsMock
                .Setup(x => x.GetAsync(BaseUrlSettingKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(baseUrl);

            var store = new LlmApiKeyStore(settingsMock.Object, encryptionMock.Object);

            // Act
            var result = await store.GetBaseUrlAsync(CancellationToken.None);

            // Assert
            Assert.Equal(baseUrl, result);
        }

        [Fact]
        public async Task GetBaseUrlAsync_ReturnsNullWhenNotSet()
        {
            // Arrange
            var (settingsMock, encryptionMock) = CreateMocks();

            settingsMock
                .Setup(x => x.GetAsync(BaseUrlSettingKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);

            var store = new LlmApiKeyStore(settingsMock.Object, encryptionMock.Object);

            // Act
            var result = await store.GetBaseUrlAsync(CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task ClearAllAsync_DeletesAllLlmSettings()
        {
            // Arrange
            var (settingsMock, encryptionMock) = CreateMocks();
            var store = new LlmApiKeyStore(settingsMock.Object, encryptionMock.Object);

            // Act
            await store.ClearAllAsync(CancellationToken.None);

            // Assert
            settingsMock.Verify(
                x => x.DeleteAsync(EncryptedApiKeySettingKey, It.IsAny<CancellationToken>()),
                Times.Once);
            settingsMock.Verify(
                x => x.DeleteAsync(ProviderSettingKey, It.IsAny<CancellationToken>()),
                Times.Once);
            settingsMock.Verify(
                x => x.DeleteAsync(ModelSettingKey, It.IsAny<CancellationToken>()),
                Times.Once);
            settingsMock.Verify(
                x => x.DeleteAsync(BaseUrlSettingKey, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task SetApiKeyAsync_PassesCancellationToken()
        {
            // Arrange
            var (settingsMock, encryptionMock) = CreateMocks();
            var cts = new CancellationTokenSource();
            var plainApiKey = "test-key";
            var encryptedKey = "encrypted";

            encryptionMock
                .Setup(x => x.EncryptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(encryptedKey);

            var store = new LlmApiKeyStore(settingsMock.Object, encryptionMock.Object);

            // Act
            await store.SetApiKeyAsync(plainApiKey, cts.Token);

            // Assert
            settingsMock.Verify(
                x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), cts.Token),
                Times.Once);
        }

        [Fact]
        public async Task GetDecryptedApiKeyAsync_PassesCancellationToken()
        {
            // Arrange
            var (settingsMock, encryptionMock) = CreateMocks();
            var cts = new CancellationTokenSource();

            settingsMock
                .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("encrypted-value");

            encryptionMock
                .Setup(x => x.DecryptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("decrypted-value");

            var store = new LlmApiKeyStore(settingsMock.Object, encryptionMock.Object);

            // Act
            await store.GetDecryptedApiKeyAsync(cts.Token);

            // Assert
            settingsMock.Verify(
                x => x.GetAsync(It.IsAny<string>(), cts.Token),
                Times.Once);
            encryptionMock.Verify(
                x => x.DecryptAsync(It.IsAny<string>(), cts.Token),
                Times.Once);
        }
    }
}
