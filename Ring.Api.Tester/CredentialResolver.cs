using System;
using System.IO;

using KoenZomers.Ring.Api;

namespace Ring.Api.Tester
{
    /// <summary>
    /// Resolved set of credentials to authenticate a <see cref="Api.Session"/> with, plus where
    /// they came from (surfaced in the index doc so a re-run failure can be diagnosed).
    /// </summary>
    internal record ResolvedCredentials(string? UserName, string? Password, string? RefreshToken, string Source);

    /// <summary>
    /// Finds credentials to authenticate with, in priority order:
    ///   1. Explicit --username/--password/--refresh-token CLI arguments
    ///   2. The encrypted auth.json at <see cref="AuthPath"/>, read via <see cref="CredentialStore"/>
    ///      (only decryptable on the same machine/user account that created it) - written by this
    ///      tool's own --auth flow, or by RingVideos
    /// No credential source is ever written to the output directory or the index doc. If none of
    /// the above resolve, run `--auth` to generate one interactively - see
    /// external/Ring.Api/README.md ("Authenticating for local tooling").
    /// </summary>
    internal static class CredentialResolver
    {
        /// <summary>
        /// The shared credentials file both this tool and the Ring.Api integration tests read from
        /// (via <see cref="CredentialStore"/>), and that `--auth` writes to. Also whatever RingVideos
        /// itself saves to once its own CredentialStore migration lands.
        /// </summary>
        public static readonly string AuthPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RingVideosData", "auth.json");

        public static ResolvedCredentials? Resolve(CliOptions options)
        {
            if (!string.IsNullOrWhiteSpace(options.RefreshToken))
            {
                return new ResolvedCredentials(options.UserName, null, options.RefreshToken, "cli-argument");
            }
            if (!string.IsNullOrWhiteSpace(options.UserName) && !string.IsNullOrWhiteSpace(options.Password))
            {
                return new ResolvedCredentials(options.UserName, options.Password, null, "cli-argument");
            }

            var saved = new CredentialStore().Load(AuthPath);
            if (!string.IsNullOrEmpty(saved.RefreshToken) || (!string.IsNullOrEmpty(saved.UserName) && !string.IsNullOrEmpty(saved.Password)))
            {
                return new ResolvedCredentials(saved.UserName, saved.Password, saved.RefreshToken, $"auth-store:{AuthPath}");
            }

            return null;
        }
    }
}
