using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
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
            RestrictToCurrentUserOnWindows(path);
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
                RestrictToCurrentUserOnWindows(filePath);

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Restricts file access to the current user on Windows to prevent credential files from being readable by other local accounts.
        /// Silently continues if ACL modification fails (e.g., sandboxed/restricted environments).
        /// </summary>
        private static void RestrictToCurrentUserOnWindows(string filePath)
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            try
            {
                var fileInfo = new FileInfo(filePath);
                var fileSecurity = fileInfo.GetAccessControl();
                fileSecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
                var currentUser = WindowsIdentity.GetCurrent().User;
                if (currentUser != null)
                {
                    fileSecurity.AddAccessRule(new FileSystemAccessRule(
                        currentUser, FileSystemRights.FullControl, AccessControlType.Allow));
                }
                fileInfo.SetAccessControl(fileSecurity);
            }
            catch
            {
                // Best-effort - a sandboxed/restricted environment (or missing System.Security.AccessControl
                // on this platform) should not crash credential save/sanitize. Matches the existing precedent
                // in data.database.sqlite/DependencyInjection/ServiceCollectionExtensions.cs.
            }
        }
    }
}
