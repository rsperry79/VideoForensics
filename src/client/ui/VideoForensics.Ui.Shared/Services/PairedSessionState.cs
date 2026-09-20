using Microsoft.JSInterop;

using System.Text.Json;

namespace VideoForensics.Ui.Shared.Services
{
    /// <summary>
    /// Circuit-scoped holder for the current paired-device session (plan §5.1/§5.11) - the WebAuthn
    /// bearer token, distinct from and unrelated to the existing Ring-account sign-in
    /// (<c>IProviderAuthService</c>). Persisted via a session cookie (see
    /// <c>wwwroot/js/webauthn.js</c>) so navigation and new tabs within the same browser session stay
    /// signed in without a fresh pairing ceremony; closing the browser itself clears the cookie and
    /// requires signing in again.
    /// </summary>
    public class PairedSessionState
    {
        private readonly IJSRuntime _js;
        private bool _loaded;

        public PairedSessionState(IJSRuntime js)
        {
            _js = js;
        }

        public string? SessionToken { get; private set; }
        public Guid? OperatorId { get; private set; }
        public string? Role { get; private set; }
        public bool MustChangePassword { get; private set; }

        public bool IsSignedIn => SessionToken is not null;

        /// <summary>Fired when the server rejects the session token as invalid or expired.</summary>
        public event Action? AuthenticationExpired;

        public async Task EnsureLoadedAsync()
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;
            try
            {
                string? json = await _js.InvokeAsync<string?>("vfWebAuthn.loadSession");
                if (string.IsNullOrEmpty(json))
                {
                    return;
                }

                StoredSession? stored = JsonSerializer.Deserialize<StoredSession>(json);
                if (stored is not null)
                {
                    SessionToken = stored.SessionToken;
                    OperatorId = stored.OperatorId;
                    Role = stored.Role;
                    MustChangePassword = stored.MustChangePassword;
                }
            }
            catch (JSException)
            {
                // Pre-render pass or a JS interop call before the circuit is fully connected - the
                // caller will simply see IsSignedIn = false until the next real load attempt.
            }
        }

        public async Task SetAsync(string sessionToken, Guid operatorId, string role, bool mustChangePassword = false)
        {
            SessionToken = sessionToken;
            OperatorId = operatorId;
            Role = role;
            MustChangePassword = mustChangePassword;
            try
            {
                string json = JsonSerializer.Serialize(new StoredSession(sessionToken, operatorId, role, mustChangePassword));
                await _js.InvokeVoidAsync("vfWebAuthn.saveSession", json);
            }
            catch (JSException)
            {
                // Same gap as EnsureLoadedAsync: wwwroot/js/webauthn.js isn't loaded on every host
                // (MAUI's BlazorWebView doesn't reference it - device pairing is server/WebApp-only
                // for now). The in-memory session above still works for this circuit; it just won't
                // survive a refresh without vfWebAuthn's localStorage persistence.
            }
        }

        public async Task ClearAsync()
        {
            SessionToken = null;
            OperatorId = null;
            Role = null;
            MustChangePassword = false;
            try
            {
                await _js.InvokeVoidAsync("vfWebAuthn.clearSession");
            }
            catch (JSException)
            {
                // See SetAsync - persistence is best-effort where vfWebAuthn isn't loaded.
            }
        }

        /// <summary>Clears the expired session and notifies listeners to re-authenticate.</summary>
        public async Task NotifyAuthenticationExpiredAsync()
        {
            await ClearAsync();
            AuthenticationExpired?.Invoke();
        }

        private record StoredSession(string SessionToken, Guid OperatorId, string Role, bool MustChangePassword);
    }
}
