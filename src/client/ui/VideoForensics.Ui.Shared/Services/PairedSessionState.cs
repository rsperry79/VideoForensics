using System.Text.Json;
using Microsoft.JSInterop;

namespace VideoForensics.Ui.Shared.Services
{
    /// <summary>
    /// Circuit-scoped holder for the current paired-device session (plan §5.1/§5.11) - the WebAuthn
    /// bearer token, distinct from and unrelated to the existing Ring-account sign-in
    /// (<c>IProviderAuthService</c>). Persisted to the browser's localStorage via
    /// <c>wwwroot/js/webauthn.js</c> so a page refresh within the same tab resumes the session
    /// without a fresh ceremony; a genuinely new tab has no localStorage entry and must re-pair.
    /// </summary>
    public class PairedSessionState
    {
        private readonly IJSRuntime _js;
        private bool _loaded;

        public PairedSessionState(IJSRuntime js) => _js = js;

        public string? SessionToken { get; private set; }
        public Guid? OperatorId { get; private set; }
        public string? Role { get; private set; }

        public bool IsSignedIn => SessionToken is not null;

        public async Task EnsureLoadedAsync()
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;
            try
            {
                var json = await _js.InvokeAsync<string?>("vfWebAuthn.loadSession");
                if (string.IsNullOrEmpty(json))
                {
                    return;
                }

                var stored = JsonSerializer.Deserialize<StoredSession>(json);
                if (stored is not null)
                {
                    SessionToken = stored.SessionToken;
                    OperatorId = stored.OperatorId;
                    Role = stored.Role;
                }
            }
            catch (JSException)
            {
                // Pre-render pass or a JS interop call before the circuit is fully connected - the
                // caller will simply see IsSignedIn = false until the next real load attempt.
            }
        }

        public async Task SetAsync(string sessionToken, Guid operatorId, string role)
        {
            SessionToken = sessionToken;
            OperatorId = operatorId;
            Role = role;
            var json = JsonSerializer.Serialize(new StoredSession(sessionToken, operatorId, role));
            await _js.InvokeVoidAsync("vfWebAuthn.saveSession", json);
        }

        public async Task ClearAsync()
        {
            SessionToken = null;
            OperatorId = null;
            Role = null;
            await _js.InvokeVoidAsync("vfWebAuthn.clearSession");
        }

        private record StoredSession(string SessionToken, Guid OperatorId, string Role);
    }
}
