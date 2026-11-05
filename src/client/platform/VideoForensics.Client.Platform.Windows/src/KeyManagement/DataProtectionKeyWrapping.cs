using System.Runtime.Versioning;

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
            if (tpmCertificateUsed)
            {
                return "TPM-held certificate";
            }

            return $"DPAPI (machine scope): TPM certificate not used (TPM available: {tpmProbeAvailable}, elevated: {elevated})";
        }
    }
}
