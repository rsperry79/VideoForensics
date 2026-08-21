using System;

namespace Ring.Api.Auth.Implementations
{
    /// <summary>
    /// Factory for creating platform-appropriate credential encryption implementations.
    /// Selects Windows DPAPI on Windows, and cross-platform AES on other platforms.
    /// </summary>
    public static class CredentialEncryptionFactory
    {
        /// <summary>
        /// Creates the default credential encryption implementation for the current platform.
        /// - Windows: Uses DPAPI (Data Protection API)
        /// - Linux/macOS: Uses AES-256 encryption
        /// </summary>
        public static ICredentialEncryption CreateDefault()
        {
            if (OperatingSystem.IsWindows())
            {
                return new WindowsDpapiEncryption();
            }

            return new AesEncryption();
        }
    }
}
