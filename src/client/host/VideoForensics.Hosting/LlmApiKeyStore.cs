using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Hosting
{
    /// <summary>
    /// Stores the LLM API key and configuration (provider, model, base URL) encrypted via the existing
    /// <see cref="ICredentialEncryptionProvider"/> abstraction. Mirrors the pattern of
    /// <see cref="ISmtpPasswordStore"/> — a single, server-wide secret with no owning account, backed by
    /// <see cref="IAppSettingRepository"/> (DB-persisted key-value) + encryption at rest.
    /// </summary>
    public interface ILlmApiKeyStore
    {
        /// <summary>Stores the LLM API key (encrypted).</summary>
        Task SetApiKeyAsync(string plainApiKey, CancellationToken ct);

        /// <summary>Retrieves the decrypted LLM API key, or null if not set.</summary>
        Task<string?> GetDecryptedApiKeyAsync(CancellationToken ct);

        /// <summary>Clears the stored API key.</summary>
        Task ClearApiKeyAsync(CancellationToken ct);

        /// <summary>Stores the LLM provider name (e.g., "Anthropic" or "OpenAiCompatible").</summary>
        Task SetProviderAsync(string provider, CancellationToken ct);

        /// <summary>Retrieves the stored LLM provider, or null if not set.</summary>
        Task<string?> GetProviderAsync(CancellationToken ct);

        /// <summary>Stores the LLM model name (e.g., "claude-3-sonnet-20240229").</summary>
        Task SetModelAsync(string model, CancellationToken ct);

        /// <summary>Retrieves the stored LLM model, or null if not set.</summary>
        Task<string?> GetModelAsync(CancellationToken ct);

        /// <summary>Stores the LLM base URL for OpenAI-compatible endpoints (nullable).</summary>
        Task SetBaseUrlAsync(string? baseUrl, CancellationToken ct);

        /// <summary>Retrieves the stored LLM base URL, or null if not set.</summary>
        Task<string?> GetBaseUrlAsync(CancellationToken ct);

        /// <summary>Clears all LLM configuration and API key.</summary>
        Task ClearAllAsync(CancellationToken ct);
    }

    public class LlmApiKeyStore : ILlmApiKeyStore
    {
        private const string EncryptedApiKeySettingKey = "Llm.EncryptedApiKey";
        private const string ProviderSettingKey = "Llm.Provider";
        private const string ModelSettingKey = "Llm.Model";
        private const string BaseUrlSettingKey = "Llm.BaseUrl";

        private readonly IAppSettingRepository _settings;
        private readonly ICredentialEncryptionProvider _encryption;

        public LlmApiKeyStore(IAppSettingRepository settings, ICredentialEncryptionProvider encryption)
        {
            _settings = settings;
            _encryption = encryption;
        }

        public async Task SetApiKeyAsync(string plainApiKey, CancellationToken ct)
        {
            string encrypted = await _encryption.EncryptAsync(plainApiKey, ct);
            await _settings.SetAsync(EncryptedApiKeySettingKey, encrypted, ct);
        }

        public async Task<string?> GetDecryptedApiKeyAsync(CancellationToken ct)
        {
            string? encrypted = await _settings.GetAsync(EncryptedApiKeySettingKey, ct);
            return string.IsNullOrEmpty(encrypted) ? null : await _encryption.DecryptAsync(encrypted, ct);
        }

        public Task ClearApiKeyAsync(CancellationToken ct)
        {
            return _settings.DeleteAsync(EncryptedApiKeySettingKey, ct);
        }

        public async Task SetProviderAsync(string provider, CancellationToken ct)
        {
            await _settings.SetAsync(ProviderSettingKey, provider, ct);
        }

        public async Task<string?> GetProviderAsync(CancellationToken ct)
        {
            return await _settings.GetAsync(ProviderSettingKey, ct);
        }

        public async Task SetModelAsync(string model, CancellationToken ct)
        {
            await _settings.SetAsync(ModelSettingKey, model, ct);
        }

        public async Task<string?> GetModelAsync(CancellationToken ct)
        {
            return await _settings.GetAsync(ModelSettingKey, ct);
        }

        public async Task SetBaseUrlAsync(string? baseUrl, CancellationToken ct)
        {
            if (baseUrl == null)
            {
                await _settings.DeleteAsync(BaseUrlSettingKey, ct);
            }
            else
            {
                await _settings.SetAsync(BaseUrlSettingKey, baseUrl, ct);
            }
        }

        public async Task<string?> GetBaseUrlAsync(CancellationToken ct)
        {
            return await _settings.GetAsync(BaseUrlSettingKey, ct);
        }

        public async Task ClearAllAsync(CancellationToken ct)
        {
            await _settings.DeleteAsync(EncryptedApiKeySettingKey, ct);
            await _settings.DeleteAsync(ProviderSettingKey, ct);
            await _settings.DeleteAsync(ModelSettingKey, ct);
            await _settings.DeleteAsync(BaseUrlSettingKey, ct);
        }
    }
}
