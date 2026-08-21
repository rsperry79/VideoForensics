using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Ring.Api.Auth.Implementations
{
    /// <summary>
    /// Cross-platform AES encryption implementation for encrypting credentials.
    /// Uses AES-256-CBC with PBKDF2 key derivation from a combination of
    /// machine identifier and user profile information.
    /// </summary>
    public class AesEncryption : ICredentialEncryption
    {
        private const int KeySize = 32; // AES-256
        private const int IvSize = 16;  // AES block size
        private const int SaltSize = 16;
        private const int Iterations = 10000; // PBKDF2 iterations

        public string Encrypt(string plaintext)
        {
            if (string.IsNullOrEmpty(plaintext))
                return null;

            try
            {
                var key = DeriveKey();
                using (var aes = Aes.Create())
                {
                    aes.KeySize = KeySize * 8;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.GenerateIV();

                    using (var encryptor = aes.CreateEncryptor(key, aes.IV))
                    using (var ms = new MemoryStream())
                    {
                        // Write IV first (needed for decryption)
                        ms.Write(aes.IV, 0, aes.IV.Length);

                        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                        using (var sw = new StreamWriter(cs, Encoding.UTF8))
                        {
                            sw.Write(plaintext);
                        }

                        return Convert.ToBase64String(ms.ToArray());
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        public string Decrypt(string ciphertext)
        {
            if (string.IsNullOrEmpty(ciphertext))
                return null;

            try
            {
                var key = DeriveKey();
                var buffer = Convert.FromBase64String(ciphertext);

                using (var aes = Aes.Create())
                {
                    aes.KeySize = KeySize * 8;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    // Extract IV from the beginning of the buffer
                    var iv = new byte[IvSize];
                    Buffer.BlockCopy(buffer, 0, iv, 0, IvSize);
                    aes.IV = iv;

                    using (var decryptor = aes.CreateDecryptor(key, aes.IV))
                    using (var ms = new MemoryStream(buffer, IvSize, buffer.Length - IvSize))
                    using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    using (var sr = new StreamReader(cs, Encoding.UTF8))
                    {
                        return sr.ReadToEnd();
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        private byte[] DeriveKey()
        {
            // Create a deterministic key based on machine and user information
            var machineId = GetMachineIdentifier();
            var userId = Environment.UserName ?? "unknown";
            var combinedInput = $"{machineId}:{userId}";

            using (var pbkdf2 = new Rfc2898DeriveBytes(
                combinedInput,
                Encoding.UTF8.GetBytes("RingVideos"),
                Iterations,
                HashAlgorithmName.SHA256))
            {
                return pbkdf2.GetBytes(KeySize);
            }
        }

        private string GetMachineIdentifier()
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    return GetWindowsMachineGuid();
                }

                if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                {
                    // On Unix-like systems, use hostname as fallback
                    return Environment.MachineName ?? System.Net.Dns.GetHostName();
                }

                return "unknown";
            }
            catch
            {
                return "unknown";
            }
        }

        private string GetWindowsMachineGuid()
        {
            try
            {
                // On Windows, try to get the unique machine GUID
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Cryptography"))
                {
                    var value = key?.GetValue("MachineGuid");
                    return value?.ToString() ?? Environment.MachineName;
                }
            }
            catch
            {
                return Environment.MachineName ?? "unknown";
            }
        }
    }
}
