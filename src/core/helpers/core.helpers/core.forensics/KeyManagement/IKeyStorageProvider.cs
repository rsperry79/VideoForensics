using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VideoForensics.Forensics.KeyManagement
{
    /// <summary>
    /// Platform-agnostic interface for storing and retrieving cryptographic keys.
    /// Implementations support TPM, DPAPI, Keychain, libsecret, and encrypted file storage.
    /// </summary>
    internal interface IKeyStorageProvider
    {
        /// <summary>
        /// Name of the key storage provider implementation.
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Indicates whether this provider is available on the current platform.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Generate a new RSA-2048 key pair for signing and store securely.
        /// </summary>
        /// <param name="keyId">Unique identifier for the key</param>
        /// <returns>Certificate thumbprint for reference</returns>
        Task<string> GenerateKeyPairAsync(string keyId);

        /// <summary>
        /// Retrieve a public key certificate by ID.
        /// </summary>
        Task<byte[]> GetPublicKeyAsync(string keyId);

        /// <summary>
        /// Sign data with the private key (never leaves secure storage).
        /// </summary>
        /// <param name="keyId">Key to use for signing</param>
        /// <param name="data">Data to sign</param>
        /// <returns>Signature bytes (base64 encoded)</returns>
        Task<string> SignDataAsync(string keyId, byte[] data);

        /// <summary>
        /// Verify a signature with the public key.
        /// </summary>
        Task<bool> VerifySignatureAsync(string keyId, byte[] data, string signature);

        /// <summary>
        /// Delete a key from storage (requires administrative authorization).
        /// </summary>
        Task DeleteKeyAsync(string keyId, string authorizingOfficer);

        /// <summary>
        /// List all stored key IDs.
        /// </summary>
        Task<IEnumerable<string>> ListKeysAsync();

        /// <summary>
        /// Export key metadata (thumbprint, creation date, algorithm info).
        /// Public key material only - never exports private key material.
        /// </summary>
        Task<KeyMetadata> GetKeyMetadataAsync(string keyId);
    }

    public class KeyMetadata
    {
        public string KeyId { get; set; } = string.Empty;
        public string CertificateThumbprint { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; }
        public DateTime? ExpiresUtc { get; set; }
        public string Algorithm { get; set; } = "RSA-2048";
        public string StorageProvider { get; set; } = string.Empty;
    }
}
