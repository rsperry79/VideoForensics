using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Hosting
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
        Task<string?> GetContactEmailAsync(CancellationToken ct);
        Task SetContactEmailAsync(string email, CancellationToken ct);
    }

    public sealed class LetsEncryptCertificateStore : ILetsEncryptCertificateStore
    {
        private const string EncryptedAccountKey = "Acme.EncryptedAccountKey";
        private const string EncryptedCertificate = "Acme.EncryptedCertificate";
        private const string CertificateNotAfter = "Acme.CertificateNotAfter";
        private const string ContactEmail = "Acme.ContactEmail";

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

        public Task<string?> GetContactEmailAsync(CancellationToken ct)
        {
            return _settings.GetAsync(ContactEmail, ct);
        }

        public Task SetContactEmailAsync(string email, CancellationToken ct)
        {
            return _settings.SetAsync(ContactEmail, email.Trim(), ct);
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
    }
}
