using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ring.Api.Forensics.KeyManagement
{
    /// <summary>
    /// Windows DPAPI (Data Protection API) based key storage provider (placeholder).
    /// Platform-specific implementation deferred. Using file-based encryption as fallback.
    /// </summary>
    internal class DpapiKeyStorageProvider : IKeyStorageProvider
    {
        public string ProviderName => "Windows DPAPI";
        public bool IsAvailable => false;

        public async Task<string> GenerateKeyPairAsync(string keyId)
        {
            throw new ForensicAnalysisException("DPAPI support not currently available");
        }

        public async Task<byte[]> GetPublicKeyAsync(string keyId)
        {
            throw new ForensicAnalysisException("DPAPI support not currently available");
        }

        public async Task<string> SignDataAsync(string keyId, byte[] data)
        {
            throw new ForensicAnalysisException("DPAPI support not currently available");
        }

        public async Task<bool> VerifySignatureAsync(string keyId, byte[] data, string signature)
        {
            throw new ForensicAnalysisException("DPAPI support not currently available");
        }

        public async Task DeleteKeyAsync(string keyId, string authorizingOfficer)
        {
            throw new ForensicAnalysisException("DPAPI support not currently available");
        }

        public async Task<IEnumerable<string>> ListKeysAsync()
        {
            return new List<string>();
        }

        public async Task<KeyMetadata> GetKeyMetadataAsync(string keyId)
        {
            return new KeyMetadata { KeyId = keyId, StorageProvider = ProviderName, CreatedUtc = DateTime.UtcNow };
        }
    }
}
