using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KoenZomers.Ring.Api
{
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

    /// <inheritdoc cref="ICredentialStore"/>
    public class CredentialStore : ICredentialStore
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

        public RingCredentials Load(string path)
        {
            if (!File.Exists(path))
            {
                return new RingCredentials();
            }

            return LoadFromJson(File.ReadAllText(path));
        }

        public RingCredentials LoadFromJson(string json)
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

        public void Save(string path, RingCredentials credentials)
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

        public void SetCredentials(string path, string userName, string password = null, string refreshToken = null)
        {
            Save(path, new RingCredentials { UserName = userName, Password = password, RefreshToken = refreshToken });
        }

        public bool SanitizeClearTextPassword(string filePath, string authPath, string clearFieldName = "Password")
        {
            if (!File.Exists(filePath))
                return false;

            try
            {
                var json = File.ReadAllText(filePath);
                var obj = JsonNode.Parse(json) as JsonObject;
                if (obj == null)
                    return false;

                if (!obj.TryGetPropertyValue(clearFieldName, out var clearValue) || clearValue == null)
                    return false;

                var clearText = clearValue.GetValue<string>();
                if (string.IsNullOrWhiteSpace(clearText))
                    return false;

                var existing = Load(authPath);
                existing.Password = clearText;
                Save(authPath, existing);

                obj.Remove(clearFieldName);
                File.WriteAllText(filePath, JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true }));

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
