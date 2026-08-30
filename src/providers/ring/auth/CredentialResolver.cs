#nullable enable

using System;
using System.IO;

using VideoForensics.Providers.Ring;
using VideoForensics.Providers.Common.Helpers.Contracts;
using VideoForensics.Providers.Common.Helpers.Platform;

namespace VideoForensics.Providers.Ring.Auth
{
    /// <summary>
    /// Resolved set of credentials to authenticate a <see cref="Session"/> with, plus where
    /// they came from (surfaced in the index doc so a re-run failure can be diagnosed).
    /// </summary>
    public record ResolvedCredentials(string? UserName, string? Password, string? RefreshToken, string Source);

    /// <summary>
    /// Finds credentials to authenticate with, in priority order:
    ///   1. Explicit username/password/refresh-token parameters
    ///   2. The encrypted auth.json at <see cref="AuthPath"/>, read via <see cref="CredentialStore"/>
    ///      (only decryptable on the same machine/user account that created it) - written by this
    ///      tool's own --auth flow
    /// No credential source is ever written to the output directory or the index doc. If none of
    /// the above resolve, authenticate interactively via 'dotnet run -- --auth' to generate and save a refresh token.
    /// </summary>
    public static class CredentialResolver
    {
        private static IPlatformDirectoryService _directoryService;

        /// <summary>
        /// The shared credentials file both this tool and the Ring.Api integration tests read from
        /// (via <see cref="CredentialStore"/>), and that interactive auth writes to. Also whatever
        /// RingVideos itself saves to once its own CredentialStore migration lands.
        /// </summary>
        public static string AuthPath =>
            Path.Combine(GetDirectoryService().GetApplicationDataDirectory(), "auth.json");

        private static IPlatformDirectoryService GetDirectoryService()
        {
            return _directoryService ??= new PlatformDirectoryService();
        }

        public static ResolvedCredentials? Resolve(string? refreshToken, string? userName, string? password)
        {
            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                return new ResolvedCredentials(userName, null, refreshToken, "cli-argument");
            }
            if (!string.IsNullOrWhiteSpace(userName) && !string.IsNullOrWhiteSpace(password))
            {
                return new ResolvedCredentials(userName, password, null, "cli-argument");
            }

            var saved = new CredentialStore().Load(AuthPath);
            if (!string.IsNullOrEmpty(saved.RefreshToken) || (!string.IsNullOrEmpty(saved.UserName) && !string.IsNullOrEmpty(saved.Password)))
            {
                return new ResolvedCredentials(saved.UserName, saved.Password, saved.RefreshToken, $"auth-store:{AuthPath}");
            }

            return null;
        }

        /// <summary>
        /// Overload that accepts an optional accountId parameter for API consistency.
        /// The accountId is accepted for forward compatibility but not used in CredentialResolver
        /// (database-based resolution happens in RingAuthService).
        /// </summary>
        public static ResolvedCredentials? Resolve(
            string? refreshToken,
            string? userName,
            string? password,
            Guid? accountId = null)
        {
            // Delegate to the existing three-parameter overload
            // accountId is here for forward compatibility but not used in filesystem-based resolution
            return Resolve(refreshToken, userName, password);
        }
    }
}
