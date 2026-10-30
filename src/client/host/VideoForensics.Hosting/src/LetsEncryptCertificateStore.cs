using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Client.Hosting
{
    /// <summary>
    /// Persists the Let's Encrypt account key and the issued certificate bundle. Both contain private key
    /// material, so they are encrypted at rest like the Cloudflare tokens. The contact email is not secret.
    /// </summary>
    public interface ILetsEncryptCertificateStore
    {
        Task<AcmeAccountState?> GetAccountAsync(CancellationToken ct);
        Task SaveAccountAsync(AcmeAccountState account, CancellationToken ct);
        Task<IssuedCertificate?> GetCertificateAsync(CancellationToken ct);
        Task SaveCertificateAsync(IssuedCertificate certificate, CancellationToken ct);
        /// <summary>Removes the stored account key and certificate. The contact email and staging choice are kept.</summary>
        Task ClearCertificateAsync(CancellationToken ct);
        Task<string?> GetContactEmailAsync(CancellationToken ct);
        Task SetContactEmailAsync(string email, CancellationToken ct);
        /// <summary>Null when never set, so the caller can fall back to the configured default.</summary>
        Task<bool?> GetUseStagingAsync(CancellationToken ct);
        Task SetUseStagingAsync(bool useStaging, CancellationToken ct);
    }

    public sealed class LetsEncryptCertificateStore : ILetsEncryptCertificateStore
    {
        private const string EncryptedAccountKey = "Acme.EncryptedAccountKey";
        private const string EncryptedCertificate = "Acme.EncryptedCertificate";
        private const string CertificateNotAfter = "Acme.CertificateNotAfter";
        private const string ContactEmail = "Acme.ContactEmail";
        private const string UseStaging = "Acme.UseStaging";

        private readonly IAppSettingRepository _settings;
        private readonly ICredentialEncryptionProvider _encryption;

        public LetsEncryptCertificateStore(IAppSettingRepository settings, ICredentialEncryptionProvider encryption)
        {
            _settings = settings;
            _encryption = encryption;
        }

        public async Task<AcmeAccountState?> GetAccountAsync(CancellationToken ct)
        {
            string? pem = await GetDecryptedAsync(EncryptedAccountKey, ct);
            return pem is null ? null : new AcmeAccountState(pem);
        }

        public Task SaveAccountAsync(AcmeAccountState account, CancellationToken ct)
        {
            return SetEncryptedAsync(EncryptedAccountKey, account.AccountKeyPem, ct);
        }

        public async Task<IssuedCertificate?> GetCertificateAsync(CancellationToken ct)
        {
            string? pem = await GetDecryptedAsync(EncryptedCertificate, ct);
            string? notAfter = await _settings.GetAsync(CertificateNotAfter, ct);
            return pem is null || !DateTimeOffset.TryParse(notAfter, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out DateTimeOffset expiry)
                ? null
                : new IssuedCertificate(pem, expiry, string.Empty);
        }

        public async Task SaveCertificateAsync(IssuedCertificate certificate, CancellationToken ct)
        {
            await SetEncryptedAsync(EncryptedCertificate, certificate.PemBundle, ct);
            await _settings.SetAsync(CertificateNotAfter, certificate.NotAfter.ToString("O", System.Globalization.CultureInfo.InvariantCulture), ct);
        }

        public async Task ClearCertificateAsync(CancellationToken ct)
        {
            await _settings.DeleteAsync(EncryptedAccountKey, ct);
            await _settings.DeleteAsync(EncryptedCertificate, ct);
            await _settings.DeleteAsync(CertificateNotAfter, ct);
        }

        public Task<string?> GetContactEmailAsync(CancellationToken ct)
        {
            return _settings.GetAsync(ContactEmail, ct);
        }

        public Task SetContactEmailAsync(string email, CancellationToken ct)
        {
            return _settings.SetAsync(ContactEmail, email.Trim(), ct);
        }

        public async Task<bool?> GetUseStagingAsync(CancellationToken ct)
        {
            string? value = await _settings.GetAsync(UseStaging, ct);
            return bool.TryParse(value, out bool useStaging) ? useStaging : null;
        }

        public Task SetUseStagingAsync(bool useStaging, CancellationToken ct)
        {
            return _settings.SetAsync(UseStaging, useStaging.ToString(), ct);
        }

        private async Task SetEncryptedAsync(string key, string plain, CancellationToken ct)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(plain);
            string encrypted = await _encryption.EncryptAsync(plain, ct);
            await _settings.SetAsync(key, encrypted, ct);
        }

        private async Task<string?> GetDecryptedAsync(string key, CancellationToken ct)
        {
            string? encrypted = await _settings.GetAsync(key, ct);
            return string.IsNullOrEmpty(encrypted) ? null : await _encryption.DecryptAsync(encrypted, ct);
        }
    }

    /// <summary>
    /// The certificate Kestrel presents on HTTPS listeners. Read on every handshake, so renewals take effect
    /// without a restart. Null means "use the server's default certificate".
    /// </summary>
    public sealed class CertificateHolder
    {
        private volatile System.Security.Cryptography.X509Certificates.X509Certificate2? _current;

        public System.Security.Cryptography.X509Certificates.X509Certificate2? Current => _current;

        public void Replace(System.Security.Cryptography.X509Certificates.X509Certificate2 certificate)
        {
            // The previous certificate is not disposed: a handshake may still be using it.
            _current = certificate;
        }

        /// <summary>Forgets the certificate so Kestrel falls back to its default. The previous one is not disposed, for the same reason as <see cref="Replace"/>.</summary>
        public void Clear()
        {
            _current = null;
        }
    }
}
