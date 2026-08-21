using System;
using System.Runtime.Versioning;
using System.Text;
using System.Security.Cryptography;

namespace Ring.Api.Auth.Implementations
{
    /// <summary>
    /// Windows-specific DPAPI (Data Protection API) implementation for encrypting credentials.
    /// This implementation leverages Windows' built-in encryption tied to the user's security context.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class WindowsDpapiEncryption : ICredentialEncryption
    {
        public string Encrypt(string plaintext)
        {
            if (string.IsNullOrEmpty(plaintext))
                return null;

            var clearBytes = Encoding.UTF8.GetBytes(plaintext);
            var encryptedBytes = ProtectedData.Protect(clearBytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedBytes);
        }

        public string Decrypt(string ciphertext)
        {
            if (string.IsNullOrEmpty(ciphertext))
                return null;

            try
            {
                var encryptedBytes = Convert.FromBase64String(ciphertext);
                var clearBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(clearBytes);
            }
            catch
            {
                return null;
            }
        }
    }
}
