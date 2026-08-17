using System;
using System.IO;
using System.Text.Json;

namespace KoenZomers.Ring.UnitTest.Mocks
{
    /// <summary>
    /// Manages Ring API credentials stored in AppData for real integration testing.
    /// Credentials are stored in: %APPDATA%/RingVideosData/test_credentials.json
    /// </summary>
    public class AppDataCredentialManager
    {
        private static readonly string CredentialsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RingVideosData"
        );

        private static readonly string CredentialsFilePath = Path.Combine(CredentialsDirectory, "test_credentials.json");

        /// <summary>
        /// Credentials stored in AppData
        /// </summary>
        public class StoredCredentials
        {
            public string? Email { get; set; }
            public string? Password { get; set; }
            public string? TwoFactorCode { get; set; }
            public DateTime? LastUpdated { get; set; }
        }

        /// <summary>
        /// Saves credentials to AppData for real integration testing
        /// </summary>
        public static void SaveCredentials(string email, string password, string? twoFactorCode = null)
        {
            try
            {
                if (!Directory.Exists(CredentialsDirectory))
                {
                    Directory.CreateDirectory(CredentialsDirectory);
                }

                var credentials = new StoredCredentials
                {
                    Email = email,
                    Password = password,
                    TwoFactorCode = twoFactorCode,
                    LastUpdated = DateTime.Now
                };

                var json = JsonSerializer.Serialize(credentials, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(CredentialsFilePath, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save credentials to AppData: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Loads credentials from AppData for real integration testing
        /// </summary>
        public static StoredCredentials? LoadCredentials()
        {
            try
            {
                if (!File.Exists(CredentialsFilePath))
                {
                    return null;
                }

                var json = File.ReadAllText(CredentialsFilePath);
                return JsonSerializer.Deserialize<StoredCredentials>(json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load credentials from AppData: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Checks if credentials are stored in AppData
        /// </summary>
        public static bool CredentialsExist() => File.Exists(CredentialsFilePath);

        /// <summary>
        /// Deletes stored credentials (useful for cleanup)
        /// </summary>
        public static void DeleteCredentials()
        {
            try
            {
                if (File.Exists(CredentialsFilePath))
                {
                    File.Delete(CredentialsFilePath);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to delete credentials: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets the path to the credentials file (for reference)
        /// </summary>
        public static string GetCredentialsPath() => CredentialsFilePath;
    }
}
