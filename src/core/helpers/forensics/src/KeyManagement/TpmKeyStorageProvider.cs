using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using VideoForensics.Core.Helpers.Forensics.Exceptions;

namespace VideoForensics.Core.Helpers.Forensics.KeyManagement
{
    /// <summary>
    /// Signing keys held by the TPM 2.0 through the Windows Platform Crypto Provider. The private key is created
    /// inside the TPM and never leaves it. Keys are machine-scoped so the service account can use them, which
    /// requires an elevated process to create or delete one.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class TpmKeyStorageProvider : IKeyStorageProvider
    {
        private const string KeyNamePrefix = "VideoForensics_";
        private const int RsaKeyBits = 2048;

        private static readonly Regex ValidKeyId = new("^[A-Za-z0-9_-]{1,64}$", RegexOptions.CultureInvariant);
        private static readonly Lazy<bool> Probe = new(ProbeTpm);

        private readonly string _manifestPath;

        public TpmKeyStorageProvider()
            : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "VideoForensics", "keys", "tpm-keys.txt"))
        {
        }

        /// <param name="manifestPath">
        /// File listing the key ids this provider created and when. CNG cannot enumerate keys, so the list is kept here.
        /// </param>
        public TpmKeyStorageProvider(string manifestPath)
        {
            _manifestPath = manifestPath;
        }

        public string ProviderName => "TPM 2.0";

        /// <summary>True when a TPM key with this id exists.</summary>
        public bool KeyExists(string keyId)
        {
            RequireAvailable();
            ValidateKeyId(keyId);
            return CngKey.Exists(KeyName(keyId), CngProvider.MicrosoftPlatformCryptoProvider, CngKeyOpenOptions.MachineKey);
        }

        /// <summary>Opens an existing TPM key. The caller disposes it.</summary>
        public CngKey OpenKey(string keyId)
        {
            RequireAvailable();
            ValidateKeyId(keyId);
            return OpenRequired(keyId);
        }

        /// <summary>True when a TPM-backed key can be created on this machine. The check runs once per process.</summary>
        public bool IsAvailable => OperatingSystem.IsWindows() && Probe.Value;

        public Task<string> GenerateKeyPairAsync(string keyId)
        {
            RequireAvailable();
            ValidateKeyId(keyId);

            using CngKey existing = OpenIfExists(keyId);
            if (existing is not null)
            {
                throw new ForensicAnalysisException($"A TPM key named '{keyId}' already exists.");
            }

            using CngKey key = CreateKey(keyId);
            string thumbprint = Thumbprint(key);
            AppendManifest(keyId, DateTime.UtcNow);
            return Task.FromResult(thumbprint);
        }

        public Task<byte[]> GetPublicKeyAsync(string keyId)
        {
            RequireAvailable();
            ValidateKeyId(keyId);

            using CngKey key = OpenRequired(keyId);
            return Task.FromResult(key.Export(CngKeyBlobFormat.GenericPublicBlob));
        }

        public Task<string> SignDataAsync(string keyId, byte[] data)
        {
            RequireAvailable();
            ValidateKeyId(keyId);
            ArgumentNullException.ThrowIfNull(data);

            using CngKey key = OpenRequired(keyId);
            using var rsa = new RSACng(key);
            byte[] signature = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return Task.FromResult(Convert.ToBase64String(signature));
        }

        public Task<bool> VerifySignatureAsync(string keyId, byte[] data, string signature)
        {
            RequireAvailable();
            ValidateKeyId(keyId);
            ArgumentNullException.ThrowIfNull(data);
            ArgumentException.ThrowIfNullOrEmpty(signature);

            using CngKey key = OpenRequired(keyId);
            using var rsa = new RSACng(key);
            bool valid = rsa.VerifyData(data, Convert.FromBase64String(signature), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return Task.FromResult(valid);
        }

        public Task DeleteKeyAsync(string keyId, string authorizingOfficer)
        {
            // Checked first: an unauthorized delete is refused whether or not the TPM is present.
            if (string.IsNullOrWhiteSpace(authorizingOfficer))
            {
                throw new ForensicAnalysisException("Deleting a TPM key requires an authorizing officer.");
            }

            RequireAvailable();
            ValidateKeyId(keyId);

            using CngKey key = OpenRequired(keyId);
            key.Delete();
            RemoveManifest(keyId);
            return Task.CompletedTask;
        }

        public Task<IEnumerable<string>> ListKeysAsync()
        {
            IEnumerable<string> ids = ReadManifest().Select(entry => entry.KeyId).ToList();
            return Task.FromResult(ids);
        }

        public Task<KeyMetadata> GetKeyMetadataAsync(string keyId)
        {
            RequireAvailable();
            ValidateKeyId(keyId);

            using CngKey key = OpenRequired(keyId);
            DateTime created = ReadManifest().FirstOrDefault(entry => entry.KeyId == keyId).CreatedUtc;
            return Task.FromResult(new KeyMetadata
            {
                KeyId = keyId,
                CertificateThumbprint = Thumbprint(key),
                CreatedUtc = created,
                Algorithm = "RSA-2048",
                StorageProvider = ProviderName,
            });
        }

        private static bool ProbeTpm()
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            string probeId = "probe" + Guid.NewGuid().ToString("N");
            try
            {
                using CngKey key = CreateKey(probeId);
                key.Delete();
                return true;
            }
            catch (CryptographicException)
            {
                return false;
            }
        }

        private static CngKey CreateKey(string keyId)
        {
            var parameters = new CngKeyCreationParameters
            {
                Provider = CngProvider.MicrosoftPlatformCryptoProvider,
                KeyUsage = CngKeyUsages.Signing,
                KeyCreationOptions = CngKeyCreationOptions.MachineKey,
            };
            parameters.Parameters.Add(new CngProperty("Length", BitConverter.GetBytes(RsaKeyBits), CngPropertyOptions.None));
            return CngKey.Create(CngAlgorithm.Rsa, KeyName(keyId), parameters);
        }

        private static string KeyName(string keyId)
        {
            return KeyNamePrefix + keyId;
        }

        private static CngKey? OpenIfExists(string keyId)
        {
            return CngKey.Exists(KeyName(keyId), CngProvider.MicrosoftPlatformCryptoProvider, CngKeyOpenOptions.MachineKey)
                ? CngKey.Open(KeyName(keyId), CngProvider.MicrosoftPlatformCryptoProvider, CngKeyOpenOptions.MachineKey)
                : null;
        }

        private static CngKey OpenRequired(string keyId)
        {
            return OpenIfExists(keyId)
                ?? throw new ForensicAnalysisException($"No TPM key named '{keyId}'.");
        }

        private static string Thumbprint(CngKey key)
        {
            byte[] publicBlob = key.Export(CngKeyBlobFormat.GenericPublicBlob);
            return Convert.ToHexString(SHA1.HashData(publicBlob));
        }

        private static void ValidateKeyId(string keyId)
        {
            if (keyId is null || !ValidKeyId.IsMatch(keyId))
            {
                throw new ArgumentException("Key ids are 1-64 letters, digits, '-' or '_'.", nameof(keyId));
            }
        }

        private void RequireAvailable()
        {
            if (!IsAvailable)
            {
                throw new ForensicAnalysisException("TPM 2.0 key storage is not available on this machine.");
            }
        }

        private readonly record struct ManifestEntry(string KeyId, DateTime CreatedUtc);

        private IEnumerable<ManifestEntry> ReadManifest()
        {
            return !File.Exists(_manifestPath)
                ? Array.Empty<ManifestEntry>()
                : File.ReadAllLines(_manifestPath)
                .Select(line => line.Split('\t'))
                .Where(parts => parts.Length == 2)
                .Select(parts => new ManifestEntry(parts[0], DateTime.Parse(parts[1], null, System.Globalization.DateTimeStyles.RoundtripKind)))
                .ToList();
        }

        private void AppendManifest(string keyId, DateTime createdUtc)
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(_manifestPath)!);
            File.AppendAllText(_manifestPath, keyId + "\t" + createdUtc.ToString("O", System.Globalization.CultureInfo.InvariantCulture) + Environment.NewLine);
        }

        private void RemoveManifest(string keyId)
        {
            if (!File.Exists(_manifestPath))
            {
                return;
            }

            IEnumerable<string> kept = File.ReadAllLines(_manifestPath).Where(line => !line.StartsWith(keyId + "\t", StringComparison.Ordinal));
            File.WriteAllLines(_manifestPath, kept);
        }
    }
}
