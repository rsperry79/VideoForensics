#nullable enable

using System;
using System.IO;

using VideoForensics.Providers.Common.Helpers.Contracts;
using VideoForensics.Providers.Common.Helpers.Platform;

namespace VideoForensics.Providers.Ring
{
    /// <summary>
    /// Resolved set of credentials to authenticate a <see cref="Session"/> with, plus where
    /// they came from (surfaced in the index doc so a re-run failure can be diagnosed).
    /// </summary>
    public record ResolvedCredentials(string? UserName, string? Password, string? RefreshToken, string Source);

    /// <summary>
    /// Finds credentials to authenticate with, in priority order:
    ///   1. Explicit username/password/refresh-token parameters
    /// Credentials are now stored exclusively in the database via RingAuthService.
    /// </summary>
    public static class CredentialResolver
    {
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

            return null;
        }

        /// <summary>
        /// Overload that accepts an optional accountId parameter for API consistency.
        /// </summary>
        public static ResolvedCredentials? Resolve(
            string? refreshToken,
            string? userName,
            string? password,
            Guid? accountId = null)
        {
            return Resolve(refreshToken, userName, password);
        }
    }
}
