using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace VideoForensics.Forensics.KeyManagement
{
    /// <summary>
    /// Factory for selecting the appropriate key storage provider based on platform availability.
    /// Tries TPM first, then platform-specific alternatives, finally encrypted file storage.
    /// </summary>
    internal static class KeyStorageFactory
    {
        public static async Task<IKeyStorageProvider> GetDefaultProviderAsync()
        {
            var providers = GetAvailableProviders();

            foreach (var provider in providers)
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

        public static IKeyStorageProvider CreateTpmProvider()
        {
            return new TpmKeyStorageProvider();
        }

        public static IKeyStorageProvider CreateDpapiProvider(string? storagePath = null)
        {
            return new DpapiKeyStorageProvider();
        }

        public static IKeyStorageProvider CreateFileBasedProvider(string storagePath)
        {
            return new FileBasedKeyStorageProvider(storagePath);
        }

        private static List<IKeyStorageProvider> GetAvailableProviders()
        {
            var providers = new List<IKeyStorageProvider>();

            // Try TPM first (most secure, hardware-based)
            try
            {
                providers.Add(new TpmKeyStorageProvider());
            }
            catch
            {
                // TPM not available
            }

            // Windows: Try DPAPI (currently placeholder)
            try
            {
                var dpapiProvider = new DpapiKeyStorageProvider();
                if (dpapiProvider.IsAvailable)
                {
                    providers.Add(dpapiProvider);
                }
            }
            catch
            {
                // DPAPI not available
            }

            // Fallback: File-based encrypted storage
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var keyStorePath = Path.Combine(appDataPath, "RingForensics", "Keys");
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
