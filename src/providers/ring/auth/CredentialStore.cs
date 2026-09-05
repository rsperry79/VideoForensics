using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

using VideoForensics.Providers.Ring.Implementations;

namespace VideoForensics.Providers.Ring
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
            return !File.Exists(path) ? new RingCredentials() : LoadFromJson(File.ReadAllText(path));
        }

        public RingCredentials LoadFromJson(string json)
        {
            try
            {
                StoredCredentials stored = JsonSerializer.Deserialize<StoredCredentials>(json);
                return stored == null
                    ? new RingCredentials()
                    : new RingCredentials
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
                _ = Directory.CreateDirectory(directory);
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
            {
                return false;
            }

            try
            {
                var json = File.ReadAllText(filePath);
                if (JsonNode.Parse(json) is not JsonObject obj)
                {
                    return false;
                }

                if (!obj.TryGetPropertyValue(clearFieldName, out JsonNode clearValue) || clearValue == null)
                {
                    return false;
                }

                var clearText = clearValue.GetValue<string>();
                if (string.IsNullOrWhiteSpace(clearText))
                {
                    return false;
                }

                RingCredentials existing = Load(authPath);
                existing.Password = clearText;
                Save(authPath, existing);

                _ = obj.Remove(clearFieldName);
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
