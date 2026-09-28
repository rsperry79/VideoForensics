using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Moq;
using VideoForensics.Api.Contracts;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Api;
using Xunit;

namespace VideoForensics.WebApp.Tests
{
    /// <summary>
    /// xUnit tests for LlmSettingsEndpoints covering GET/POST for LLM provider configuration.
    /// Tests verify API key handling (no retrieval/exposure), optional base URL, and provider validation.
    /// </summary>
    public class LlmSettingsEndpointsTests
    {
        private static Mock<ILlmApiKeyStore> CreateMockKeyStore()
        {
            return new Mock<ILlmApiKeyStore>();
        }

        private static Mock<ILogger<Program>> CreateMockLogger()
        {
            return new Mock<ILogger<Program>>();
        }

        private static HttpContext CreateDefaultHttpContext()
        {
            return new DefaultHttpContext();
        }

        [Fact]
        public async Task GetAsync_WithValidConfiguration_ReturnsSettingsWithoutApiKey()
        {
            // Arrange
            var keyStore = CreateMockKeyStore();
            keyStore.Setup(k => k.GetProviderAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync("Anthropic");
            keyStore.Setup(k => k.GetModelAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync("claude-3-sonnet-20240229");
            keyStore.Setup(k => k.GetBaseUrlAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);

            var logger = CreateMockLogger();

            // Act
            var result = await LlmSettingsEndpointsInvoker.GetAsync(keyStore.Object, logger.Object, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            var okResult = result as Ok<LlmSettingsResponseDto>;
            Assert.NotNull(okResult);
            var responseDto = okResult.Value;
            Assert.NotNull(responseDto);
            Assert.Equal("Anthropic", responseDto.Provider);
            Assert.Equal("claude-3-sonnet-20240229", responseDto.Model);
            Assert.Empty(responseDto.BaseUrl); // null is converted to empty string

            keyStore.Verify(k => k.GetProviderAsync(It.IsAny<CancellationToken>()), Times.Once);
            keyStore.Verify(k => k.GetModelAsync(It.IsAny<CancellationToken>()), Times.Once);
            keyStore.Verify(k => k.GetBaseUrlAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetAsync_NoConfigurationSet_ReturnsDefaults()
        {
            // Arrange
            var keyStore = CreateMockKeyStore();
            keyStore.Setup(k => k.GetProviderAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);
            keyStore.Setup(k => k.GetModelAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);
            keyStore.Setup(k => k.GetBaseUrlAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);

            var logger = CreateMockLogger();

            // Act
            var result = await LlmSettingsEndpointsInvoker.GetAsync(keyStore.Object, logger.Object, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            var okResult = result as Ok<LlmSettingsResponseDto>;
            Assert.NotNull(okResult);
            var responseDto = okResult.Value;
            Assert.NotNull(responseDto);
            Assert.Empty(responseDto.Provider);
            Assert.Empty(responseDto.Model);
            Assert.Empty(responseDto.BaseUrl);
        }

        [Fact]
        public async Task PostAsync_WithValidRequestAndNewApiKey_UpdatesAllSettings()
        {
            // Arrange
            var request = new LlmSettingsRequestDto(
                Provider: "Anthropic",
                Model: "claude-3-sonnet-20240229",
                ApiKey: "sk-ant-test-key-12345",
                BaseUrl: null
            );

            var keyStore = CreateMockKeyStore();
            keyStore.Setup(k => k.SetProviderAsync("Anthropic", It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            keyStore.Setup(k => k.SetModelAsync("claude-3-sonnet-20240229", It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            keyStore.Setup(k => k.SetApiKeyAsync("sk-ant-test-key-12345", It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            keyStore.Setup(k => k.SetBaseUrlAsync(null, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            keyStore.Setup(k => k.GetProviderAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync("Anthropic");
            keyStore.Setup(k => k.GetModelAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync("claude-3-sonnet-20240229");
            keyStore.Setup(k => k.GetBaseUrlAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);

            var logger = CreateMockLogger();

            // Act
            var result = await LlmSettingsEndpointsInvoker.PostAsync(request, keyStore.Object, logger.Object, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            var okResult = result as Ok<LlmSettingsResponseDto>;
            Assert.NotNull(okResult);
            var responseDto = okResult.Value;
            Assert.NotNull(responseDto);
            Assert.Equal("Anthropic", responseDto.Provider);
            Assert.Equal("claude-3-sonnet-20240229", responseDto.Model);

            keyStore.Verify(k => k.SetProviderAsync("Anthropic", It.IsAny<CancellationToken>()), Times.Once);
            keyStore.Verify(k => k.SetModelAsync("claude-3-sonnet-20240229", It.IsAny<CancellationToken>()), Times.Once);
            keyStore.Verify(k => k.SetApiKeyAsync("sk-ant-test-key-12345", It.IsAny<CancellationToken>()), Times.Once);
            keyStore.Verify(k => k.SetBaseUrlAsync(null, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PostAsync_WithNullApiKey_LeavesExistingKeyUntouched()
        {
            // Arrange
            var request = new LlmSettingsRequestDto(
                Provider: "Anthropic",
                Model: "claude-3-sonnet-20240229",
                ApiKey: null,
                BaseUrl: null
            );

            var keyStore = CreateMockKeyStore();
            keyStore.Setup(k => k.SetProviderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            keyStore.Setup(k => k.SetModelAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            keyStore.Setup(k => k.SetBaseUrlAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            keyStore.Setup(k => k.GetProviderAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync("Anthropic");
            keyStore.Setup(k => k.GetModelAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync("claude-3-sonnet-20240229");
            keyStore.Setup(k => k.GetBaseUrlAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);

            var logger = CreateMockLogger();

            // Act
            var result = await LlmSettingsEndpointsInvoker.PostAsync(request, keyStore.Object, logger.Object, CancellationToken.None);

            // Assert
            Assert.NotNull(result);

            keyStore.Verify(k => k.SetApiKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task PostAsync_WithEmptyApiKey_LeavesExistingKeyUntouched()
        {
            // Arrange
            var request = new LlmSettingsRequestDto(
                Provider: "Anthropic",
                Model: "claude-3-sonnet-20240229",
                ApiKey: string.Empty,
                BaseUrl: null
            );

            var keyStore = CreateMockKeyStore();
            keyStore.Setup(k => k.SetProviderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            keyStore.Setup(k => k.SetModelAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            keyStore.Setup(k => k.SetBaseUrlAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            keyStore.Setup(k => k.GetProviderAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync("Anthropic");
            keyStore.Setup(k => k.GetModelAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync("claude-3-sonnet-20240229");
            keyStore.Setup(k => k.GetBaseUrlAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);

            var logger = CreateMockLogger();

            // Act
            var result = await LlmSettingsEndpointsInvoker.PostAsync(request, keyStore.Object, logger.Object, CancellationToken.None);

            // Assert
            Assert.NotNull(result);

            keyStore.Verify(k => k.SetApiKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task PostAsync_WithValidRequestAndNullBaseUrl_ClearsBaseUrl()
        {
            // Arrange
            var request = new LlmSettingsRequestDto(
                Provider: "OpenAiCompatible",
                Model: "gpt-4",
                ApiKey: "sk-test",
                BaseUrl: null
            );

            var keyStore = CreateMockKeyStore();
            keyStore.Setup(k => k.SetProviderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            keyStore.Setup(k => k.SetModelAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            keyStore.Setup(k => k.SetApiKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            keyStore.Setup(k => k.SetBaseUrlAsync(null, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            keyStore.Setup(k => k.GetProviderAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync("OpenAiCompatible");
            keyStore.Setup(k => k.GetModelAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync("gpt-4");
            keyStore.Setup(k => k.GetBaseUrlAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);

            var logger = CreateMockLogger();

            // Act
            var result = await LlmSettingsEndpointsInvoker.PostAsync(request, keyStore.Object, logger.Object, CancellationToken.None);

            // Assert
            Assert.NotNull(result);

            keyStore.Verify(k => k.SetBaseUrlAsync(null, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PostAsync_InvalidProvider_ReturnsBadRequest()
        {
            // Arrange
            var request = new LlmSettingsRequestDto(
                Provider: "InvalidProvider",
                Model: "some-model",
                ApiKey: "sk-test",
                BaseUrl: null
            );

            var keyStore = CreateMockKeyStore();
            var logger = CreateMockLogger();

            // Act
            var result = await LlmSettingsEndpointsInvoker.PostAsync(request, keyStore.Object, logger.Object, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            var statusResult = result as IStatusCodeHttpResult;
            Assert.NotNull(statusResult);
            Assert.Equal(StatusCodes.Status400BadRequest, statusResult.StatusCode);
        }

        [Fact]
        public async Task PostAsync_MissingRequiredFields_ReturnsBadRequest()
        {
            // Arrange - null provider
            var requestNullProvider = new LlmSettingsRequestDto(
                Provider: null,
                Model: "some-model",
                ApiKey: "sk-test",
                BaseUrl: null
            );

            var keyStore = CreateMockKeyStore();
            var logger = CreateMockLogger();

            // Act
            var result = await LlmSettingsEndpointsInvoker.PostAsync(requestNullProvider, keyStore.Object, logger.Object, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            var statusResult = result as IStatusCodeHttpResult;
            Assert.NotNull(statusResult);
            Assert.Equal(StatusCodes.Status400BadRequest, statusResult.StatusCode);
        }

        [Fact]
        public async Task PostAsync_MissingModel_ReturnsBadRequest()
        {
            // Arrange - empty model
            var requestEmptyModel = new LlmSettingsRequestDto(
                Provider: "Anthropic",
                Model: string.Empty,
                ApiKey: "sk-test",
                BaseUrl: null
            );

            var keyStore = CreateMockKeyStore();
            var logger = CreateMockLogger();

            // Act
            var result = await LlmSettingsEndpointsInvoker.PostAsync(requestEmptyModel, keyStore.Object, logger.Object, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            var statusResult = result as IStatusCodeHttpResult;
            Assert.NotNull(statusResult);
            Assert.Equal(StatusCodes.Status400BadRequest, statusResult.StatusCode);
        }
    }

    /// <summary>
    /// Helper class to invoke private methods in LlmSettingsEndpoints for testing.
    /// Uses reflection to call the private static async methods.
    /// </summary>
    internal static class LlmSettingsEndpointsInvoker
    {
        public static async Task<IResult> GetAsync(
            ILlmApiKeyStore keyStore,
            ILogger<Program> logger,
            CancellationToken ct)
        {
            var method = typeof(LlmSettingsEndpoints)
                .GetMethod("GetLlmSettingsAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                    null,
                    [typeof(ILlmApiKeyStore), typeof(ILogger<Program>), typeof(CancellationToken)],
                    null);

            if (method == null)
                throw new InvalidOperationException("Could not find GetLlmSettingsAsync method");

            var result = method.Invoke(null, [keyStore, logger, ct]);
            return await (Task<IResult>)result!;
        }

        public static async Task<IResult> PostAsync(
            LlmSettingsRequestDto request,
            ILlmApiKeyStore keyStore,
            ILogger<Program> logger,
            CancellationToken ct)
        {
            var method = typeof(LlmSettingsEndpoints)
                .GetMethod("UpdateLlmSettingsAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                    null,
                    [typeof(LlmSettingsRequestDto), typeof(ILlmApiKeyStore), typeof(ILogger<Program>), typeof(CancellationToken)],
                    null);

            if (method == null)
                throw new InvalidOperationException("Could not find UpdateLlmSettingsAsync method");

            var result = method.Invoke(null, [request, keyStore, logger, ct]);
            return await (Task<IResult>)result!;
        }
    }
}
