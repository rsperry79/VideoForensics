using System;
using System.Threading.Tasks;

namespace VideoForensics.Providers.Ring
{
    public partial class Session
    {
        /// <summary>
        /// Authenticates using the given credentials, preferring a saved refresh token and falling
        /// back to username/password (with an optional two-factor code prompt) if no refresh token is
        /// present or it's no longer valid. On success, <paramref name="credentials"/>.RefreshToken is
        /// updated in place with the new refresh token, so the caller can persist it (e.g. via
        /// <see cref="CredentialStore.Save"/>) for next time.
        /// </summary>
        /// <param name="credentials">Credentials to authenticate with. RefreshToken is updated in place on success.</param>
        /// <param name="twoFactorAuthCodeProvider">Called to retrieve a two-factor code from the user when the account requires it. If null, <see cref="Exceptions.TwoFactorAuthenticationRequiredException"/> propagates instead of prompting.</param>
        /// <param name="progress">Optional progress notifications for UI feedback (e.g. spinner text). Purely informational - callers should not rely on it for control flow.</param>
        /// <param name="messageHandler">Optional custom HttpMessageHandler for testing purposes</param>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when there's no valid refresh token and no username/password to fall back to, or the username/password are invalid.</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationIncorrectException">Thrown when the provided two-factor code was incorrect.</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationRequiredException">Thrown when two-factor authentication is required and no <paramref name="twoFactorAuthCodeProvider"/> was given.</exception>
        public static async Task<Session> AuthenticateWithCredentials(
            RingCredentials credentials,
            Func<Task<string>> twoFactorAuthCodeProvider = null,
            IProgress<AuthProgressEventArgs> progress = null,
            System.Net.Http.HttpMessageHandler messageHandler = null)
        {
            if (credentials == null)
            {
                throw new ArgumentNullException(nameof(credentials));
            }

            Session session = null;

            if (!string.IsNullOrWhiteSpace(credentials.RefreshToken))
            {
                try
                {
                    progress?.Report(new AuthProgressEventArgs("Authenticating using refresh token from previous session..."));
                    session = await GetSessionByRefreshToken(credentials.RefreshToken, messageHandler);
                    progress?.Report(new AuthProgressEventArgs("Refresh token authentication succeeded"));
                }
                catch (Exception ex)
                {
                    progress?.Report(new AuthProgressEventArgs($"Refresh token authentication failed: {ex.Message}", isWarning: true));
                    session = null;
                }
            }

            if (session == null)
            {
                if (string.IsNullOrWhiteSpace(credentials.UserName) || string.IsNullOrWhiteSpace(credentials.Password))
                {
                    throw new Exceptions.AuthenticationFailedException(
                        "No valid refresh token and no username/password provided to authenticate with");
                }

                session = new Session(credentials.UserName, credentials.Password, messageHandler);
                progress?.Report(new AuthProgressEventArgs("Authenticating using provided username and password..."));

                try
                {
                    await session.Authenticate();
                }
                catch (Exceptions.TwoFactorAuthenticationRequiredException)
                {
                    if (twoFactorAuthCodeProvider == null)
                    {
                        throw;
                    }

                    progress?.Report(new AuthProgressEventArgs("Two-factor authentication required"));
                    var code = await twoFactorAuthCodeProvider();
                    await session.Authenticate(twoFactorAuthCode: code);
                }
            }

            if (session?.OAuthToken != null)
            {
                credentials.RefreshToken = session.OAuthToken.RefreshToken;
            }

            return session;
        }
    }
}
