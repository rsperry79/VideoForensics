using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Hosting
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

        public CloudflareTokenStore(
            IAppSettingRepository settings,
            ICredentialEncryptionProvider encryption)
        {
            _settings = settings;
            _encryption = encryption;
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
            return string.IsNullOrEmpty(encrypted)
                ? null
                : await _encryption.DecryptAsync(encrypted, ct);
        }
    }
}
