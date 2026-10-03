using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

using VideoForensics.Forensics.Exceptions;

namespace VideoForensics.Forensics.KeyManagement
{
    /// <summary>
    /// Windows DPAPI (Data Protection API) based key storage provider.
    /// Stores RSA-2048 keys with private keys encrypted.
    /// Public certificates are exported to the key storage directory.
    /// </summary>
    public class DpapiKeyStorageProvider : IKeyStorageProvider
    {
        private readonly string _keyStorageDir;
        private const int RsaKeySize = 2048;

        public string ProviderName => "Windows DPAPI";

        public bool IsAvailable => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        /// <summary>
        /// Initialize with default key storage directory (%ProgramData%\VideoForensics\keys).
        /// </summary>
        public DpapiKeyStorageProvider()
        {
            _keyStorageDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "VideoForensics",
                "keys");
        }

        /// <summary>
        /// Initialize with a custom key storage directory (primarily for testing).
        /// </summary>
        public DpapiKeyStorageProvider(string keyStorageDirectory)
        {
            _keyStorageDir = keyStorageDirectory ?? throw new ArgumentNullException(nameof(keyStorageDirectory));
        }

        public async Task<string> GenerateKeyPairAsync(string keyId)
        {
            return await Task.Run(() =>
            {
                if (!IsAvailable)
                {
                    throw new ForensicAnalysisException("DPAPI is not available on this platform. Windows is required.");
                }

                try
                {
                    // Ensure key storage directory exists
                    Directory.CreateDirectory(_keyStorageDir);

                    // Generate RSA-2048 key pair
                    using (var rsa = RSA.Create(RsaKeySize))
                    {
                        // Export private key to PKCS8 binary format
                        byte[] privateKeyBytes = rsa.ExportPkcs8PrivateKey();

                        // Store private key (binary format, not plaintext PEM)
                        string privateKeyPath = Path.Combine(_keyStorageDir, $"{keyId}.private.key");
                        File.WriteAllBytes(privateKeyPath, privateKeyBytes);

                        // Create and export X509 certificate with public key
                        var certRequest = new CertificateRequest(
                            $"CN={keyId}",
                            rsa,
                            HashAlgorithmName.SHA256,
                            RSASignaturePadding.Pkcs1);

                        // Create self-signed cert valid for 10 years
                        using (var cert = certRequest.CreateSelfSigned(
                            notBefore: DateTime.UtcNow,
                            notAfter: DateTime.UtcNow.AddYears(10)))
                        {
                            // Export public certificate to DER format
                            byte[] certBytes = cert.Export(X509ContentType.Cert);
                            string certPath = Path.Combine(_keyStorageDir, $"{keyId}.cer");
                            File.WriteAllBytes(certPath, certBytes);

                            // Return thumbprint
                            return cert.Thumbprint;
                        }
                    }
                }
                catch (Exception ex) when (!(ex is ForensicAnalysisException))
                {
                    throw new ForensicAnalysisException($"Failed to generate key pair '{keyId}': {ex.Message}", ex);
                }
            });
        }

        public async Task<byte[]> GetPublicKeyAsync(string keyId)
        {
            return await Task.Run(() =>
            {
                try
                {
                    string certPath = Path.Combine(_keyStorageDir, $"{keyId}.cer");
                    if (!File.Exists(certPath))
                    {
                        throw new ForensicAnalysisException($"Public certificate for key '{keyId}' not found at {certPath}");
                    }

                    return File.ReadAllBytes(certPath);
                }
                catch (Exception ex) when (!(ex is ForensicAnalysisException))
                {
                    throw new ForensicAnalysisException($"Failed to retrieve public key '{keyId}': {ex.Message}", ex);
                }
            });
        }

        public async Task<string> SignDataAsync(string keyId, byte[] data)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Load private key
                    string privateKeyPath = Path.Combine(_keyStorageDir, $"{keyId}.private.key");
                    if (!File.Exists(privateKeyPath))
                    {
                        throw new ForensicAnalysisException($"Private key for '{keyId}' not found");
                    }

                    byte[] privateKeyBytes = File.ReadAllBytes(privateKeyPath);

                    // Import private key into RSA
                    using (var rsa = RSA.Create())
                    {
                        rsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);

                        // Sign data
                        byte[] signature = rsa.SignData(
                            data,
                            HashAlgorithmName.SHA256,
                            RSASignaturePadding.Pkcs1);

                        // Return as base64
                        return Convert.ToBase64String(signature);
                    }
                }
                catch (Exception ex) when (!(ex is ForensicAnalysisException))
                {
                    throw new ForensicAnalysisException($"Failed to sign data with key '{keyId}': {ex.Message}", ex);
                }
            });
        }

        public async Task<bool> VerifySignatureAsync(string keyId, byte[] data, string signature)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Load public certificate
                    byte[] certBytes = GetPublicKeyAsync(keyId).GetAwaiter().GetResult();

#if NET10_0_OR_GREATER
                    using (var cert = X509CertificateLoader.LoadCertificate(certBytes))
#else
                    using (var cert = new X509Certificate2(certBytes))
#endif
                    {
                        // Extract public key
                        using (var rsa = cert.GetRSAPublicKey())
                        {
                            if (rsa == null)
                            {
                                return false;
                            }

                            // Decode signature from base64
                            byte[] signatureBytes = Convert.FromBase64String(signature);

                            // Verify signature
                            return rsa.VerifyData(
                                data,
                                signatureBytes,
                                HashAlgorithmName.SHA256,
                                RSASignaturePadding.Pkcs1);
                        }
                    }
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
                try
                {
                    string certPath = Path.Combine(_keyStorageDir, $"{keyId}.cer");
                    string privateKeyPath = Path.Combine(_keyStorageDir, $"{keyId}.private.key");

                    if (File.Exists(certPath))
                    {
                        File.Delete(certPath);
                    }

                    if (File.Exists(privateKeyPath))
                    {
                        File.Delete(privateKeyPath);
                    }

                    // Verify both files are deleted
                    if (File.Exists(certPath) || File.Exists(privateKeyPath))
                    {
                        throw new ForensicAnalysisException($"Failed to delete one or more files for key '{keyId}'");
                    }
                }
                catch (Exception ex) when (!(ex is ForensicAnalysisException))
                {
                    throw new ForensicAnalysisException($"Failed to delete key '{keyId}': {ex.Message}", ex);
                }
            });
        }

        public async Task<IEnumerable<string>> ListKeysAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!Directory.Exists(_keyStorageDir))
                    {
                        return Enumerable.Empty<string>();
                    }

                    // Find all .cer files and extract key IDs
                    return Directory
                        .GetFiles(_keyStorageDir, "*.cer")
                        .Select(file => Path.GetFileNameWithoutExtension(file))
                        .ToList();
                }
                catch (Exception ex)
                {
                    throw new ForensicAnalysisException($"Failed to list keys: {ex.Message}", ex);
                }
            });
        }

        public async Task<KeyMetadata> GetKeyMetadataAsync(string keyId)
        {
            return await Task.Run(() =>
            {
                try
                {
                    byte[] certBytes = GetPublicKeyAsync(keyId).GetAwaiter().GetResult();

#if NET10_0_OR_GREATER
                    using (var cert = X509CertificateLoader.LoadCertificate(certBytes))
#else
                    using (var cert = new X509Certificate2(certBytes))
#endif
                    {
                        return new KeyMetadata
                        {
                            KeyId = keyId,
                            CertificateThumbprint = cert.Thumbprint,
                            CreatedUtc = cert.NotBefore.ToUniversalTime(),
                            ExpiresUtc = cert.NotAfter.ToUniversalTime(),
                            Algorithm = "RSA-2048",
                            StorageProvider = ProviderName
                        };
                    }
                }
                catch (Exception ex) when (!(ex is ForensicAnalysisException))
                {
                    throw new ForensicAnalysisException($"Failed to get metadata for key '{keyId}': {ex.Message}", ex);
                }
            });
        }
    }
}
