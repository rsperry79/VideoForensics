using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Ring.Api.Forensics.KeyManagement
{
    /// <summary>
    /// File-based encrypted key storage provider using AES-256-GCM.
    /// Used as fallback when TPM and platform-specific providers are unavailable.
    /// Keys are encrypted at rest with AES-256-GCM.
    /// </summary>
    internal class FileBasedKeyStorageProvider : IKeyStorageProvider
    {
        private readonly string _keyStorePath;
        private readonly string _masterKeyPath;
        private byte[]? _masterKey;
        private const string KeyFileExtension = ".key";
        private const string MasterKeyFile = "master.key";

        public string ProviderName => "File-Based Encrypted Storage";
        public bool IsAvailable => true; // Always available as fallback

        public FileBasedKeyStorageProvider(string storagePath)
        {
            _keyStorePath = storagePath;
            _masterKeyPath = Path.Combine(_keyStorePath, MasterKeyFile);
            Directory.CreateDirectory(_keyStorePath);
        }

        public async Task<string> GenerateKeyPairAsync(string keyId)
        {
            return await Task.Run(() =>
            {
                using var rsa = RSA.Create(2048);
                var publicKey = rsa.ExportSubjectPublicKeyInfo();
                var privateKey = rsa.ExportPkcs8PrivateKey();

                var thumbprint = ComputeThumbprint(publicKey);

                var encryptedPrivateKey = EncryptKey(privateKey, keyId);

                var keyPath = Path.Combine(_keyStorePath, $"{keyId}{KeyFileExtension}");
                File.WriteAllBytes(keyPath, encryptedPrivateKey);

                var pubKeyPath = Path.Combine(_keyStorePath, $"{keyId}.pub");
                File.WriteAllBytes(pubKeyPath, publicKey);

                StoreKeyMetadata(keyId, thumbprint);

                return thumbprint;
            });
        }

        public async Task<byte[]> GetPublicKeyAsync(string keyId)
        {
            return await Task.Run(() =>
            {
                var pubKeyPath = Path.Combine(_keyStorePath, $"{keyId}.pub");
                if (File.Exists(pubKeyPath))
                {
                    return File.ReadAllBytes(pubKeyPath);
                }
                return Array.Empty<byte>();
            });
        }

        public async Task<string> SignDataAsync(string keyId, byte[] data)
        {
            return await Task.Run(() =>
            {
                var keyPath = Path.Combine(_keyStorePath, $"{keyId}{KeyFileExtension}");
                if (!File.Exists(keyPath))
                {
                    throw new ForensicAnalysisException($"Key {keyId} not found");
                }

                var encryptedPrivateKey = File.ReadAllBytes(keyPath);
                var privateKey = DecryptKey(encryptedPrivateKey, keyId);

                using var rsa = RSA.Create();
                rsa.ImportPkcs8PrivateKey(privateKey, out _);

                var signature = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                return Convert.ToBase64String(signature);
            });
        }

        public async Task<bool> VerifySignatureAsync(string keyId, byte[] data, string signature)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var publicKeyBytes = GetPublicKeyAsync(keyId).Result;
                    if (publicKeyBytes.Length == 0)
                    {
                        return false;
                    }

                    using var rsa = RSA.Create();
                    rsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);

                    var signatureBytes = Convert.FromBase64String(signature);
                    return rsa.VerifyData(data, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                }
                catch
                {
                    return false;
                }
            });
        }

        public async Task DeleteKeyAsync(string keyId, string authorizingOfficer)
        {
            await Task.Run(() =>
            {
                var keyPath = Path.Combine(_keyStorePath, $"{keyId}{KeyFileExtension}");
                var pubKeyPath = Path.Combine(_keyStorePath, $"{keyId}.pub");
                var metadataPath = Path.Combine(_keyStorePath, $"{keyId}.meta");

                if (File.Exists(keyPath))
                {
                    File.Delete(keyPath);
                }
                if (File.Exists(pubKeyPath))
                {
                    File.Delete(pubKeyPath);
                }
                if (File.Exists(metadataPath))
                {
                    File.Delete(metadataPath);
                }
            });
        }

        public async Task<IEnumerable<string>> ListKeysAsync()
        {
            return await Task.Run(() =>
            {
                if (!Directory.Exists(_keyStorePath))
                {
                    return new List<string>();
                }

                return Directory.GetFiles(_keyStorePath, $"*{KeyFileExtension}")
                    .Select(f => Path.GetFileNameWithoutExtension(f))
                    .ToList();
            });
        }

        public async Task<KeyMetadata> GetKeyMetadataAsync(string keyId)
        {
            return await Task.Run(() =>
            {
                var metadata = RetrieveKeyMetadata(keyId);
                return metadata ?? new KeyMetadata
                {
                    KeyId = keyId,
                    StorageProvider = ProviderName,
                    CreatedUtc = DateTime.UtcNow
                };
            });
        }

        private byte[] EncryptKey(byte[] keyData, string keyId)
        {
            var masterKey = GetOrCreateMasterKey();

            using var aes = new AesGcm(masterKey, 16);
            var nonce = new byte[12];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(nonce);

            var tag = new byte[16];
            var ciphertext = new byte[keyData.Length];

            var associatedData = Encoding.UTF8.GetBytes(keyId);
            aes.Encrypt(nonce, keyData, ciphertext, tag, associatedData);

            // Return: nonce + tag + ciphertext
            var result = new byte[nonce.Length + tag.Length + ciphertext.Length];
            Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
            Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
            Buffer.BlockCopy(ciphertext, 0, result, nonce.Length + tag.Length, ciphertext.Length);

            return result;
        }

        private byte[] DecryptKey(byte[] encryptedData, string keyId)
        {
            var masterKey = GetOrCreateMasterKey();

            const int nonceLength = 12;
            const int tagLength = 16;

            var nonce = new byte[nonceLength];
            var tag = new byte[tagLength];
            var ciphertext = new byte[encryptedData.Length - nonceLength - tagLength];

            Buffer.BlockCopy(encryptedData, 0, nonce, 0, nonceLength);
            Buffer.BlockCopy(encryptedData, nonceLength, tag, 0, tagLength);
            Buffer.BlockCopy(encryptedData, nonceLength + tagLength, ciphertext, 0, ciphertext.Length);

            using var aes = new AesGcm(masterKey, 16);
            var plaintext = new byte[ciphertext.Length];

            var associatedData = Encoding.UTF8.GetBytes(keyId);
            aes.Decrypt(nonce, ciphertext, tag, plaintext, associatedData);

            return plaintext;
        }

        private byte[] GetOrCreateMasterKey()
        {
            if (_masterKey != null)
            {
                return _masterKey;
            }

            if (File.Exists(_masterKeyPath))
            {
                _masterKey = File.ReadAllBytes(_masterKeyPath);
            }
            else
            {
                _masterKey = new byte[32]; // 256 bits for AES-256
                using var rng = RandomNumberGenerator.Create();
                rng.GetBytes(_masterKey);

                File.WriteAllBytes(_masterKeyPath, _masterKey);
                File.SetAttributes(_masterKeyPath, FileAttributes.Hidden);
            }

            return _masterKey;
        }

        private string ComputeThumbprint(byte[] publicKey)
        {
            var hash = SHA256.HashData(publicKey);
            return Convert.ToHexString(hash);
        }

        private void StoreKeyMetadata(string keyId, string thumbprint)
        {
            var metadata = new KeyMetadata
            {
                KeyId = keyId,
                CertificateThumbprint = thumbprint,
                CreatedUtc = DateTime.UtcNow,
                StorageProvider = ProviderName,
                Algorithm = "RSA-2048"
            };

            var metadataPath = Path.Combine(_keyStorePath, $"{keyId}.meta");
            var json = JsonSerializer.Serialize(metadata);
            File.WriteAllText(metadataPath, json);
        }

        private KeyMetadata? RetrieveKeyMetadata(string keyId)
        {
            var metadataPath = Path.Combine(_keyStorePath, $"{keyId}.meta");
            if (File.Exists(metadataPath))
            {
                try
                {
                    var json = File.ReadAllText(metadataPath);
                    return JsonSerializer.Deserialize<KeyMetadata>(json);
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }
    }
}
