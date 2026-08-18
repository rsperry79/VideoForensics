using System;
using System.Threading.Tasks;

namespace KoenZomers.Ring.Api
{
    /// <summary>
    /// Authenticates a <see cref="Session"/> with a username/password, transparently handling a
    /// two-factor challenge if the account requires one - without depending on any particular
    /// console app or UI. Callers supply how to obtain the 2FA code (console prompt, test fixture,
    /// whatever); this class only owns the retry-with-code mechanics against the Ring API itself.
    /// Used by ApiTester's --auth flow and available to anything else (tests included) that needs
    /// to bootstrap a session/refresh token without going through RingVideos.
    /// </summary>
    public static class InteractiveAuth
    {
        /// <summary>
        /// Authenticates a new session for the given username/password. If the account requires
        /// two-factor authentication, <paramref name="getTwoFactorCode"/> is invoked (after Ring
        /// has sent the code via text/e-mail) to obtain the code and a second attempt is made.
        /// </summary>
        /// <param name="userName">Ring account username/email</param>
        /// <param name="password">Ring account password</param>
        /// <param name="getTwoFactorCode">Called only if Ring requires 2FA; should return the code once the caller has it</param>
        /// <returns>The authenticated session, with a valid OAuthToken (including refresh token) on success</returns>
        public static async Task<Session> AuthenticateAsync(string userName, string password, Func<Task<string>> getTwoFactorCode)
        {
            var session = new Session(userName, password);

            try
            {
                await session.Authenticate();
            }
            catch (Exceptions.TwoFactorAuthenticationRequiredException)
            {
                var code = await getTwoFactorCode();
                await session.Authenticate(twoFactorAuthCode: code);
            }

            return session;
        }
    }
}
