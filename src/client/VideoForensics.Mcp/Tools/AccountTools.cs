using System.ComponentModel;
using ModelContextProtocol.Server;
using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.Mcp.Tools
{
    /// <summary>MCP tools for Ring account authentication and credential management.</summary>
    [McpServerToolType]
    public static class AccountTools
    {
        // Holds the username/password from an Authenticate call that came back needing a 2FA code,
        // so a subsequent SubmitTwoFactorCode call can complete the same login. MCP tool calls are
        // independent request/response round trips (unlike the console app's synchronous prompt), so
        // this two-step flow is modeled as two separate tool calls sharing this small piece of state
        // rather than a blocking callback.
        private static (string Username, string Password)? _pendingTwoFactorLogin;

        [McpServerTool, Description("Authenticates with the Ring account using a username and password. If the account requires two-factor authentication, this returns a message asking the caller to invoke SubmitTwoFactorCode with the code sent to the account holder (e.g. via SMS).")]
        public static async Task<string> Authenticate(
            IProviderAuthService authService,
            [Description("Ring account email address")] string username,
            [Description("Ring account password")] string password,
            CancellationToken cancellationToken)
        {
            var result = await authService.AuthenticateAsync(username, password, cancellationToken);
            if (result.Success)
            {
                _pendingTwoFactorLogin = null;
                return "Authentication successful.";
            }

            if (!string.IsNullOrEmpty(result.ErrorMessage) &&
                result.ErrorMessage.Contains("two-factor", StringComparison.OrdinalIgnoreCase))
            {
                _pendingTwoFactorLogin = (username, password);
                return "Two-factor authentication is required. Call SubmitTwoFactorCode with the code sent to the account holder to complete authentication.";
            }

            _pendingTwoFactorLogin = null;
            return $"Authentication failed: {result.ErrorMessage}";
        }

        [McpServerTool, Description("Submits a two-factor authentication code to complete an Authenticate call that returned a 2FA-required response. Call Authenticate first.")]
        public static async Task<string> SubmitTwoFactorCode(
            IProviderAuthService authService,
            [Description("The 2FA code sent to the account holder")] string code,
            CancellationToken cancellationToken)
        {
            if (_pendingTwoFactorLogin is not { } pending)
            {
                return "No authentication is currently awaiting a 2FA code. Call Authenticate first.";
            }

            var result = await authService.AuthenticateWithTwoFactorAsync(
                pending.Username,
                pending.Password,
                () => Task.FromResult(code),
                cancellationToken);

            _pendingTwoFactorLogin = null;

            return result.Success
                ? "Authentication successful."
                : $"Authentication failed: {result.ErrorMessage}";
        }

        [McpServerTool, Description("Attempts to restore a previously authenticated session from saved (encrypted) credentials, without requiring the user to re-enter a password.")]
        public static async Task<string> RestoreSavedCredentials(
            IProviderAuthService authService,
            CancellationToken cancellationToken)
        {
            var restored = await authService.RestoreFromSavedCredentialsAsync(cancellationToken);
            return restored
                ? "Session restored from saved credentials."
                : "No saved credentials were available, or they could not be used to restore a session. Call Authenticate.";
        }
    }
}
