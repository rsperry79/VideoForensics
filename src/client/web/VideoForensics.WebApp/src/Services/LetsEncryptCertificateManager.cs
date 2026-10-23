using System.Security.Cryptography.X509Certificates;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.WebApp.Services;
using VideoForensics.Client.Hosting;

namespace VideoForensics.Client.WebApp.Services
{

    /// <summary>
    /// Keeps a trusted certificate for the configured Internet hostname. Issues one through Let's Encrypt
    /// DNS-01 when none exists or the current one is near expiry, and loads the stored certificate into the
    /// holder Kestrel reads, so a renewal takes effect without a restart.
    /// </summary>
    public sealed partial class LetsEncryptCertificateManager(
        ICloudflareTokenStore tokens,
        ICloudflareApiClient cloudflare,
        ILetsEncryptCertificateStore store,
        IAcmeCertificateIssuer issuer,
        CertificateHolder holder,
        IForensicsConfiguration config,
        IConfiguration configuration,
        ILogger<LetsEncryptCertificateManager> logger)
    {
        private readonly ICloudflareTokenStore _tokens = tokens;
        private readonly ICloudflareApiClient _cloudflare = cloudflare;
        private readonly ILetsEncryptCertificateStore _store = store;
        private readonly IAcmeCertificateIssuer _issuer = issuer;
        private readonly CertificateHolder _holder = holder;
        private readonly IForensicsConfiguration _config = config;
        private readonly IConfiguration _configuration = configuration;
        private readonly ILogger<LetsEncryptCertificateManager> _logger = logger;

        /// <summary>
        /// Loads the stored certificate, then issues a new one if <paramref name="force"/> is set or renewal is due.
        /// Acme:UseStaging defaults to true, so production issuance is an explicit configuration change.
        /// </summary>
        public async Task<CertificateCheckResult> EnsureCertificateAsync(bool force, CancellationToken ct)
        {
            IssuedCertificate? stored = await _store.GetCertificateAsync(ct).ConfigureAwait(true);
            if (stored is not null)
            {
                _holder.Replace(ToX509(stored));
            }

            if (!force && !CertificateRenewalPolicy.IsRenewalDue(stored?.NotAfter, DateTimeOffset.UtcNow))
            {
                return new CertificateCheckResult(true, false, "The current certificate is still valid.");
            }

            if (!Uri.TryCreate(_config.InternetServerUrl, UriKind.Absolute, out Uri? internetUrl) || (internetUrl.Host.Length == 0))
            {
                return new CertificateCheckResult(false, false, "Configure an Internet address first.");
            }

            string? apiToken = await _tokens.GetDecryptedApiTokenAsync(ct).ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(apiToken))
            {
                return new CertificateCheckResult(false, false, "Configure a Cloudflare API token first.");
            }

            string? email = await _store.GetContactEmailAsync(ct).ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(email))
            {
                return new CertificateCheckResult(false, false, "Enter a contact email for Let's Encrypt.");
            }

            CloudflareZoneResult zones = await _cloudflare.ValidateTokenAndListZonesAsync(apiToken, ct).ConfigureAwait(true);
            if (!zones.IsValid)
            {
                return new CertificateCheckResult(false, false, zones.ErrorMessage ?? "Cloudflare rejected the stored API token.");
            }

            CloudflareZone? zone = CertificateRenewalPolicy.FindZoneForHost(zones.Zones, internetUrl.Host);
            if (zone is null)
            {
                return new CertificateCheckResult(false, false, $"No zone visible to the Cloudflare token covers {internetUrl.Host}.");
            }

            bool useStaging = _configuration.GetValue("Acme:UseStaging", true);
            AcmeAccountState? account = await _store.GetAccountAsync(ct).ConfigureAwait(true);
            var publisher = new CloudflareDnsChallengePublisher(_cloudflare, apiToken, zone.Id);

            _logger.LogInformation("Requesting a {Environment}  ertificate for {Host}", useStaging ? "staging" : "production", internetUrl.Host);
            IssuedCertificate issued = await _issuer.IssueAsync(internetUrl.Host, email, account, useStaging, publisher, ct).ConfigureAwait(true);

            await _store.SaveAccountAsync(new AcmeAccountState(issued.AccountKeyPem), ct).ConfigureAwait(true);
            await _store.SaveCertificateAsync(issued, ct).ConfigureAwait(true);
            _holder.Replace(ToX509(issued));

            _logger.LogInformation("Issued a {Environment} certificate for {Host}, valid until {NotAfter:u}", useStaging ? "staging" : "production", internetUrl.Host, issued.NotAfter);
            return new CertificateCheckResult(true, true, $"Certificate issued for {internetUrl.Host}, valid until {issued.NotAfter:yyyy-MM-dd}.");
        }

        /// <summary>
        /// Re-imports the PEM bundle as a PKCS#12 certificate with an ephemeral key. Kestrel on Windows needs the
        /// private key in that form; a PEM-only certificate fails the handshake.
        /// </summary>
        internal static X509Certificate2 ToX509(IssuedCertificate certificate)
        {
            // The single-string overload does not accept a bundle holding both the certificate and its key,
            // so the leaf certificate and key blocks are split out and passed separately.
            string certPem = ExtractPemBlock(certificate.PemBundle, "CERTIFICATE");
            string keyPem = ExtractPemBlock(certificate.PemBundle, "PRIVATE KEY");
            using var pem = X509Certificate2.CreateFromPem(certPem, keyPem);
            return X509CertificateLoader.LoadPkcs12(pem.Export(X509ContentType.Pfx), (string?)null, X509KeyStorageFlags.EphemeralKeySet);
        }

        /// <summary>Returns the first PEM block whose label ends with <paramref name="label"/>.</summary>
        private static string ExtractPemBlock(string bundle, string label)
        {
            foreach (System.Text.RegularExpressions.Match match in CertRegex().Matches(bundle))
            {
                if (match.Groups[1].Value.EndsWith(label, StringComparison.Ordinal))
                {
                    return match.Value;
                }
            }

            throw new InvalidOperationException($"The certificate bundle has no {label} block.");
        }

        [System.Text.RegularExpressions.GeneratedRegex(@"-----BEGIN ([A-Z0-9 ]+)-----[\s\S]*?-----END \1-----")]
        private static partial System.Text.RegularExpressions.Regex CertRegex();
    }
}
