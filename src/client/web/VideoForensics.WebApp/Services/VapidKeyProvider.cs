using System.Text.Json;
using VideoForensics.Data.Common.Contracts;
using WebPush;

namespace VideoForensics.WebApp.Services
{
    /// <summary>
    /// Manages VAPID (Voluntary Application Server Identification) key pairs for Web Push notifications.
    /// Keys are persisted in the application settings database and auto-generated on first access.
    /// </summary>
    public class VapidKeyProvider
    {
        private readonly IAppSettingRepository _appSettingRepository;

        public VapidKeyProvider(IAppSettingRepository appSettingRepository)
        {
            _appSettingRepository = appSettingRepository ?? throw new ArgumentNullException(nameof(appSettingRepository));
        }

        /// <summary>
        /// Retrieves stored VAPID key pair or generates a new one if either key is missing.
        /// Keys are persisted to the database on generation for use across service restarts.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Tuple of (PublicKey, PrivateKey).</returns>
        public async Task<(string PublicKey, string PrivateKey)> GetOrCreateKeysAsync(CancellationToken ct)
        {
            const string publicKeyName = "WebPush.VapidPublicKey";
            const string privateKeyName = "WebPush.VapidPrivateKey";

            // Try to load existing keys
            string? publicKey = await _appSettingRepository.GetAsync(publicKeyName, ct);
            string? privateKey = await _appSettingRepository.GetAsync(privateKeyName, ct);

            // If either key is missing, generate a new pair
            if (string.IsNullOrEmpty(publicKey) || string.IsNullOrEmpty(privateKey))
            {
                var vapidDetails = VapidHelper.GenerateVapidKeys();
                publicKey = vapidDetails.PublicKey;
                privateKey = vapidDetails.PrivateKey;

                // Persist both keys to the database
                await _appSettingRepository.SetAsync(publicKeyName, publicKey, ct);
                await _appSettingRepository.SetAsync(privateKeyName, privateKey, ct);
            }

            return (publicKey, privateKey);
        }
    }
}
