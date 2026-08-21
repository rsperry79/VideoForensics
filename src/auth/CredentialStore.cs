using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ring.Api.Auth;
using Ring.Api.Auth.Implementations;

namespace Ring.Api
{
    /// <summary>
    /// On-disk (encrypted) representation of <see cref="RingCredentials"/>.
    /// Encrypts credentials using platform-appropriate encryption (DPAPI on Windows, AES on Linux/macOS).
    /// </summary>
    internal class StoredCredentials
    {
        public string UserName { get; set; }
        public string Password { get; set; }
        public string RefreshToken { get; set; }
    }

    /// <inheritdoc cref="ICredentialStore"/>
    public class CredentialStore : ICredentialStore
    {
        private readonly ICredentialEncryption _encryption;

        public CredentialStore(ICredentialEncryption encryption = null)
        {
            _encryption = encryption ?? CredentialEncryptionFactory.CreateDefault();
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
                if (stored == null)
                {
                    return new RingCredentials();
                }

                return new RingCredentials
                {
                    UserName = stored.UserName,
                    Password = _encryption.Decrypt(stored.Password),
                    RefreshToken = _encryption.Decrypt(stored.RefreshToken)
                };
            }
            catch
            {
                return new RingCredentials();
            }
        }

        public void Save(string path, RingCredentials credentials)
        {
            var stored = new StoredCredentials
            {
                UserName = credentials.UserName,
                Password = _encryption.Encrypt(credentials.Password),
                RefreshToken = _encryption.Encrypt(credentials.RefreshToken)
            };

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, JsonSerializer.Serialize(stored, new JsonSerializerOptions { WriteIndented = true }));
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
