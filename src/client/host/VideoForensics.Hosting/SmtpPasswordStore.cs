using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Hosting
{
    /// <summary>
    /// Stores the SMTP password for email notifications (plan §5.6) encrypted via the existing
    /// <see cref="ICredentialEncryptionProvider"/> abstraction, per plan §4.1's rule that every new
    /// secret this plan introduces routes through that one mechanism rather than a new plaintext
    /// setting. Deliberately separate from <see cref="ICredentialRepository"/> (which is
    /// provider-account-scoped, for Ring/Wyze OAuth credentials) - this is a single,
    /// server-wide secret with no owning account, so it is its own tiny store instead of being
    /// force-fit into that schema.
    /// </summary>
    public interface ISmtpPasswordStore
    {
        Task SetAsync(string plainPassword, CancellationToken ct);
        Task<string?> GetDecryptedAsync(CancellationToken ct);
        Task ClearAsync(CancellationToken ct);
    }

    public class SmtpPasswordStore : ISmtpPasswordStore
    {
        private const string SettingKey = "Smtp.EncryptedPassword";

        private readonly IAppSettingRepository _settings;
        private readonly ICredentialEncryptionProvider _encryption;

        public SmtpPasswordStore(IAppSettingRepository settings, ICredentialEncryptionProvider encryption)
        {
            _settings = settings;
            _encryption = encryption;
        }

        public async Task SetAsync(string plainPassword, CancellationToken ct)
        {
            var encrypted = await _encryption.EncryptAsync(plainPassword, ct);
            await _settings.SetAsync(SettingKey, encrypted, ct);
        }

        public async Task<string?> GetDecryptedAsync(CancellationToken ct)
        {
            var encrypted = await _settings.GetAsync(SettingKey, ct);
            return string.IsNullOrEmpty(encrypted) ? null : await _encryption.DecryptAsync(encrypted, ct);
        }

        public Task ClearAsync(CancellationToken ct) => _settings.DeleteAsync(SettingKey, ct);
    }
}
