using System;

namespace VideoForensics.Providers.Ring.Implementations
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
            return OperatingSystem.IsWindows() ? new WindowsDpapiEncryption() : new AesEncryption();
        }
    }
}
