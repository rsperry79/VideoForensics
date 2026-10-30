using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;

using VideoForensics.Core.Helpers.Forensics.Exceptions;

namespace VideoForensics.Core.Helpers.Forensics.KeyManagement
{
    /// <summary>
    /// Registration point for platform-specific key storage providers. A host sets <see cref="PlatformProviders"/> at startup;
    /// the Windows host supplies TPM and DPAPI, so Core carries no Windows key storage code.
    /// </summary>
    public static class KeyStorageProviders
    {
        /// <summary>Returns the platform providers in priority order. Null means none are registered.</summary>
        public static Func<IEnumerable<IKeyStorageProvider>>? PlatformProviders { get; set; }
    }

    /// <summary>
    /// Factory for selecting the appropriate key storage provider based on platform availability.
    /// Tries TPM first, then platform-specific alternatives, finally encrypted file storage.
    /// </summary>
    internal static class KeyStorageFactory
    {
        public static async Task<IKeyStorageProvider> GetDefaultProviderAsync()
        {
            List<IKeyStorageProvider> providers = GetAvailableProviders();

            foreach (IKeyStorageProvider provider in providers)
            {
                if (provider.IsAvailable)
                {
                    return provider;
                }
            }

            throw new ForensicAnalysisException(
                "No suitable key storage provider found. " +
                "TPM 2.0, Windows DPAPI, or file-based encryption required.");
        }

        public static IKeyStorageProvider CreateFileBasedProvider(string storagePath)
        {
            return new FileBasedKeyStorageProvider(storagePath);
        }

        private static List<IKeyStorageProvider> GetAvailableProviders()
        {
            var providers = new List<IKeyStorageProvider>();

            // Platform providers (TPM, then DPAPI on Windows) come from the host that registered them, in the order it lists them.
            // Core has no Windows code, so without a registration only the file-based fallback is available.
            if (KeyStorageProviders.PlatformProviders is { } platformProviders)
            {
                try
                {
                    foreach (IKeyStorageProvider provider in platformProviders())
                    {
                        if (provider.IsAvailable)
                        {
                            providers.Add(provider);
                        }
                    }
                }
                catch
                {
                    // A platform provider failed to initialize; the file-based fallback below still applies.
                }
            }

            // Fallback: File-based encrypted storage
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string keyStorePath = Path.Combine(appDataPath, "VideoForensics", "keys");
            try
            {
                providers.Add(new FileBasedKeyStorageProvider(keyStorePath));
            }
            catch
            {
                // File-based storage not available
            }

            return providers;
        }
    }
}
