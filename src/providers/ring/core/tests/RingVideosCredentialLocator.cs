using System;
using System.IO;

using VideoForensics.Providers.Ring;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>
    /// Discovers and decrypts credentials saved by the RingVideos application, so the integration
    /// tests can authenticate automatically when App.config has no credentials of its own.
    /// Delegates to <see cref="CredentialStore"/>, the same reader/writer the RingVideos app uses.
    /// </summary>
    internal static class RingVideosCredentialLocator
    {
        private static readonly string AuthPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RingVideosData", "auth.json");

        public static bool TryLoad(out string? userName, out string? password, out string? refreshToken)
        {
            var saved = new CredentialStore().Load(AuthPath);
            userName = saved.UserName;
            password = saved.Password;
            refreshToken = saved.RefreshToken;

            return !string.IsNullOrEmpty(refreshToken) || (!string.IsNullOrEmpty(userName) && !string.IsNullOrEmpty(password));
        }
    }
}
