using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace KoenZomers.Ring.Api
{
    /// <summary>
    /// Plaintext Ring account credentials, as loaded from or about to be saved to disk via <see cref="CredentialStore"/>.
    /// </summary>
    public class RingCredentials
    {
        public string UserName { get; set; }
        public string Password { get; set; }
        public string RefreshToken { get; set; }
    }

    /// <summary>
    /// On-disk (encrypted) representation of <see cref="RingCredentials"/>.
    /// </summary>
    internal class StoredCredentials
    {
        public string UserName { get; set; }
        public string Password { get; set; }
        public string RefreshToken { get; set; }
        public string EncryptionIV { get; set; }
    }

    /// <summary>
    /// Reads and writes Ring account credentials (username, password, refresh token) to a JSON file
    /// of the caller's choosing, encrypted with a key derived from the current machine and user
    /// account so the file is only readable on the machine/account that wrote it. Callers only need
    /// to supply a file path; this class owns the storage format and encryption.
    /// </summary>
    public static class CredentialStore
    {
        private const string KeySuffix = "453nfawehfaypg94#$#@%34wghvoawe[cwe45a3wtg";
        private const string Salt = "$2a$04$qdxi1jNcjqWBlsviWGilx.Xxw0oMm0gZYx8ZsLq5ntsy5s4GFq3kq";

        private static byte[] DeriveKey()
        {
            return Rfc2898DeriveBytes.Pbkdf2(
                Encoding.ASCII.GetBytes(Environment.MachineName + Environment.UserName + KeySuffix),
                Encoding.ASCII.GetBytes(Salt),
                100,
                HashAlgorithmName.SHA1,
                32);
        }

        /// <summary>
        /// Loads credentials from the given file path. Returns an empty <see cref="RingCredentials"/>
        /// if the file doesn't exist or can't be decrypted (e.g. written on a different machine/account).
        /// </summary>
        public static RingCredentials Load(string path)
        {
            if (!File.Exists(path))
            {
                return new RingCredentials();
            }

            return LoadFromJson(File.ReadAllText(path));
        }

        /// <summary>
        /// Decrypts credentials from a raw JSON string in the storage format written by <see cref="Save"/>.
        /// Returns an empty <see cref="RingCredentials"/> if the JSON is malformed or can't be decrypted.
        /// </summary>
        public static RingCredentials LoadFromJson(string json)
        {
            try
            {
                var stored = JsonSerializer.Deserialize<StoredCredentials>(json);
                if (stored == null || string.IsNullOrWhiteSpace(stored.EncryptionIV))
                {
                    return new RingCredentials();
                }

                using var aes = Aes.Create();
                aes.IV = Convert.FromBase64String(stored.EncryptionIV);
                aes.Key = DeriveKey();
                using var decryptor = aes.CreateDecryptor();

                return new RingCredentials
                {
                    UserName = stored.UserName,
                    Password = string.IsNullOrEmpty(stored.Password) ? null : Decrypt(decryptor, stored.Password),
                    RefreshToken = string.IsNullOrEmpty(stored.RefreshToken) ? null : Decrypt(decryptor, stored.RefreshToken)
                };
            }
            catch
            {
                return new RingCredentials();
            }
        }

        /// <summary>
        /// Encrypts and saves credentials to the given file path, creating the parent directory if needed.
        /// </summary>
        public static void Save(string path, RingCredentials credentials)
        {
            using var aes = Aes.Create();
            aes.GenerateIV();
            aes.Key = DeriveKey();
            using var encryptor = aes.CreateEncryptor();

            var stored = new StoredCredentials
            {
                UserName = credentials.UserName,
                Password = string.IsNullOrEmpty(credentials.Password) ? null : Encrypt(encryptor, credentials.Password),
                RefreshToken = string.IsNullOrEmpty(credentials.RefreshToken) ? null : Encrypt(encryptor, credentials.RefreshToken),
                EncryptionIV = Convert.ToBase64String(aes.IV)
            };

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, JsonSerializer.Serialize(stored, new JsonSerializerOptions { WriteIndented = true }));
        }

        private static string Encrypt(ICryptoTransform encryptor, string clearText)
        {
            var clearBytes = Encoding.UTF8.GetBytes(clearText);
            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            {
                cs.Write(clearBytes, 0, clearBytes.Length);
                cs.FlushFinalBlock();
            }
            return Convert.ToBase64String(ms.ToArray());
        }

        private static string Decrypt(ICryptoTransform decryptor, string base64)
        {
            var bytes = Convert.FromBase64String(base64);
            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Write))
            {
                cs.Write(bytes, 0, bytes.Length);
                cs.FlushFinalBlock();
            }
            return Encoding.UTF8.GetString(ms.ToArray());
        }
    }
}
