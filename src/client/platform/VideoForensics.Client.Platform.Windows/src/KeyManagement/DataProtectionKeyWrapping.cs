using System.Runtime.Versioning;
using System.Security.Cryptography.X509Certificates;

namespace VideoForensics.Client.Platform.Windows.KeyManagement
{
    /// <summary>
    /// Describes how the ASP.NET Data Protection key ring is wrapped at startup, for the startup log. The TPM certificate is
    /// preferred; when it is not used, the probe result and elevation are named so a fallback to DPAPI can be explained.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class DataProtectionKeyWrapping
    {
        /// <summary>Returns a one-line description of the wrapping chosen for this process.</summary>
        /// <param name="tpmCertificateUsed">True when the TPM-held certificate wraps the key ring.</param>
        /// <param name="tpmProbeAvailable">Whether this process could create and delete a TPM key (the probe).</param>
        /// <param name="elevated">Whether this process is running elevated. The probe's key creation needs it.</param>
        public static string Describe(bool tpmCertificateUsed, bool tpmProbeAvailable, bool elevated)
        {
            return tpmCertificateUsed
                ? "TPM-held certificate"
                : $"DPAPI (machine scope): TPM certificate not used (TPM available: {tpmProbeAvailable}, elevated: {elevated})";
        }

        /// <summary>
        /// Returns the TPM certificate once it loads, retrying after each delay. When every attempt fails and the machine
        /// already has the TPM data-protection key, this throws instead of letting the caller fall back to DPAPI: keys wrapped
        /// with DPAPI would not be readable on a machine whose TPM comes back. Returns null only when no TPM key exists, so a
        /// machine that never had a TPM key can still start on DPAPI.
        /// </summary>
        /// <param name="tryLoad">Loads or creates the TPM certificate. Null when the TPM is not usable.</param>
        /// <param name="tpmKeyExists">True when the machine already has the TPM data-protection key.</param>
        /// <param name="wait">Waits between attempts.</param>
        /// <param name="delays">The wait before each retry. Attempts are one more than this count.</param>
        public static X509Certificate2? ResolveTpmCertificate(
            Func<X509Certificate2?> tryLoad,
            Func<bool> tpmKeyExists,
            Action<TimeSpan> wait,
            IReadOnlyList<TimeSpan> delays)
        {
            for (int attempt = 0; ; attempt++)
            {
                X509Certificate2? certificate = tryLoad();
                if (certificate is not null)
                {
                    return certificate;
                }

                if (attempt >= delays.Count)
                {
                    break;
                }

                wait(delays[attempt]);
            }

            return tpmKeyExists()
                ? throw new InvalidOperationException("The TPM data-protection key exists but could not be used after retries. Refusing to start rather than wrap keys with DPAPI. Check the TPM and the service account, then restart.")
                : null;
        }
    }
}
