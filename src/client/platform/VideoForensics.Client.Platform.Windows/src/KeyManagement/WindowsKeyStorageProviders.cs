using System;
using System.Collections.Generic;

using VideoForensics.Core.Helpers.Forensics.KeyManagement;

namespace VideoForensics.Client.Platform.Windows.KeyManagement
{
    /// <summary>
    /// Builds the Windows key storage providers for <see cref="KeyStorageProviders.PlatformProviders"/>, in priority order:
    /// TPM 2.0 first (hardware-backed), then DPAPI. A provider that cannot initialize is skipped rather than failing the list.
    /// </summary>
    public static class WindowsKeyStorageProviders
    {
        /// <summary>Returns the TPM and DPAPI providers on Windows, and nothing elsewhere.</summary>
        public static IEnumerable<IKeyStorageProvider> Create()
        {
            if (!OperatingSystem.IsWindows())
            {
                yield break;
            }

            IKeyStorageProvider? tpm = TryCreateTpm();
            if (tpm is not null)
            {
                yield return tpm;
            }

            IKeyStorageProvider? dpapi = TryCreate(() => new DpapiKeyStorageProvider());
            if (dpapi is not null)
            {
                yield return dpapi;
            }
        }

        /// <summary>TPM exists only on Windows, so its construction is confined to a method the caller has already guarded.</summary>
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static IKeyStorageProvider? TryCreateTpm()
        {
            return TryCreate(() => new TpmKeyStorageProvider());
        }

        private static IKeyStorageProvider? TryCreate(Func<IKeyStorageProvider> create)
        {
            try
            {
                return create();
            }
            catch
            {
                // Provider unavailable on this machine (no TPM, DPAPI error); the caller carries on without it.
                return null;
            }
        }
    }
}
