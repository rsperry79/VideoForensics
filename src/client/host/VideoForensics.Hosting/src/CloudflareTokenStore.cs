using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using System.Security.Cryptography;

using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Client.Hosting
{
    public interface ICloudflareTokenStore
    {
        Task SetApiTokenAsync(string plainToken, CancellationToken ct);
        Task<string?> GetDecryptedApiTokenAsync(CancellationToken ct);
        Task SetTunnelTokenAsync(string plainToken, CancellationToken ct);
        Task<string?> GetDecryptedTunnelTokenAsync(CancellationToken ct);
        Task ClearAsync(CancellationToken ct);
    }

    public sealed class CloudflareTokenStore : ICloudflareTokenStore
    {
        private const string EncryptedApiTokenSettingKey = "Cloudflare.EncryptedApiToken";
        private const string EncryptedTunnelTokenSettingKey = "Cloudflare.EncryptedTunnelToken";

        private readonly IAppSettingRepository _settings;
        private readonly ICredentialEncryptionProvider _encryption;
        private readonly ILogger<CloudflareTokenStore> _logger;

        public CloudflareTokenStore(
            IAppSettingRepository settings,
            ICredentialEncryptionProvider encryption,
            ILogger<CloudflareTokenStore>? logger = null)
        {
            _settings = settings;
            _encryption = encryption;
            _logger = logger ?? NullLogger<CloudflareTokenStore>.Instance;
        }

        public Task SetApiTokenAsync(string plainToken, CancellationToken ct)
        {
            return SetEncryptedAsync(EncryptedApiTokenSettingKey, plainToken, ct);
        }

        public Task SetTunnelTokenAsync(string plainToken, CancellationToken ct)
        {
            return SetEncryptedAsync(EncryptedTunnelTokenSettingKey, plainToken, ct);
        }

        public Task<string?> GetDecryptedApiTokenAsync(CancellationToken ct)
        {
            return GetDecryptedAsync(EncryptedApiTokenSettingKey, ct);
        }

        public Task<string?> GetDecryptedTunnelTokenAsync(CancellationToken ct)
        {
            return GetDecryptedAsync(EncryptedTunnelTokenSettingKey, ct);
        }

        public async Task ClearAsync(CancellationToken ct)
        {
            await _settings.DeleteAsync(EncryptedApiTokenSettingKey, ct);
            await _settings.DeleteAsync(EncryptedTunnelTokenSettingKey, ct);
        }

        private async Task SetEncryptedAsync(string key, string plainToken, CancellationToken ct)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(plainToken);
            string encrypted = await _encryption.EncryptAsync(plainToken, ct);
            await _settings.SetAsync(key, encrypted, ct);
        }

        private async Task<string?> GetDecryptedAsync(string key, CancellationToken ct)
        {
            string? encrypted = await _settings.GetAsync(key, ct);
            if (string.IsNullOrEmpty(encrypted))
            {
                return null;
            }

            // The key ring can lose the key that sealed this value (wiped or regenerated key folder). The token is
            // then unrecoverable, so report it as not configured. Throwing here would fail the whole status endpoint.
            try
            {
                return await _encryption.DecryptAsync(encrypted, ct);
            }
            catch (CryptographicException ex)
            {
                _logger.LogError(
                    "Stored Cloudflare value {SettingKey} could not be decrypted ({ExceptionType}): {Message}. Re-enter the token in Remote Access settings.",
                    key,
                    ex.GetType().Name,
                    ex.Message);
                return null;
            }
        }
    }
}
