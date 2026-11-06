using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace VideoForensics.Client.Platform.Windows.KeyManagement
{
    /// <summary>
    /// A certificate whose private key lives in the TPM, used to protect ASP.NET Data Protection keys. The key ring
    /// is encrypted to this certificate's public key, and only the TPM can decrypt it.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class TpmDataProtectionCertificate
    {
        /// <summary>The TPM key id reserved for data protection.</summary>
        public const string KeyId = "DataProtection";

        /// <summary>
        /// True when the machine already has the data-protection TPM key. A TPM that cannot be read counts as no key, so the
        /// caller decides the policy.
        /// </summary>
        public static bool KeyExists()
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            try
            {
                return new TpmKeyStorageProvider().KeyExists(KeyId);
            }
            catch (CryptographicException)
            {
                return false;
            }
        }

        /// <summary>
        /// Returns a certificate backed by the TPM key, creating the key on first use. Returns null when the TPM is
        /// not usable, so the caller can fall back to DPAPI. Creating the key needs an elevated process.
        /// </summary>
        public static X509Certificate2? TryLoadOrCreate()
        {
            if (!OperatingSystem.IsWindows())
            {
                return null;
            }

            var provider = new TpmKeyStorageProvider();
            if (!provider.IsAvailable)
            {
                return null;
            }

            if (!provider.KeyExists(KeyId))
            {
                _ = provider.GenerateKeyPairAsync(KeyId).GetAwaiter().GetResult();
            }

            CngKey key = provider.OpenKey(KeyId); // Kept open: the returned certificate relies on this key handle.
            using var rsa = new RSACng(key);
            var request = new CertificateRequest("CN=VideoForensics data protection", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyEncipherment, critical: true));
            using X509Certificate2 selfSigned = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(20));

            // The returned copy holds a handle to the TPM key, so decryption at startup still signs and decrypts in the TPM.
            return X509CertificateLoader.LoadCertificate(selfSigned.Export(X509ContentType.Cert)).CopyWithPrivateKey(new RSACng(key));
        }
    }
}
