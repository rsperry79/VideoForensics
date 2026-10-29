using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Ui.Shared.Services
{
    /// <summary>
    /// Circuit-scoped holder for the current paired-device session (plan §5.1/§5.11) - the WebAuthn
    /// bearer token, distinct from and unrelated to the existing Ring-account sign-in
    /// (<c>IProviderAuthService</c>). Persisted via a session cookie (see
    /// <c>wwwroot/scripts/webauthn.js</c>) so navigation and new tabs within the same browser session stay
    /// signed in without a fresh pairing ceremony; closing the browser itself clears the cookie and
    /// requires signing in again.
    /// </summary>
    public class PairedSessionState
    {
        private readonly IJSRuntime _js;
        private readonly IServiceProvider? _services;
        private Task? _loadTask;

        /// <summary>
        /// Without services the server check in <see cref="EnsureLoadedAsync"/> is skipped and only the stored
        /// flag is used. The DI-registered constructor below is the one hosts use.
        /// </summary>
        public PairedSessionState(IJSRuntime js)
        {
            _js = js;
        }

        /// <summary>
        /// Resolves <see cref="ISelfApiHttpClientFactory"/> lazily, not in the constructor: the WebApp's factory
        /// depends on this class (for the session token), so injecting it here would be a circular dependency.
        /// </summary>
        public PairedSessionState(IJSRuntime js, IServiceProvider services)
            : this(js)
        {
            _services = services;
        }

        public virtual string? SessionToken { get; private set; }
        public virtual Guid? OperatorId { get; private set; }
        public virtual string? Role { get; private set; }
        public virtual bool MustChangePassword { get; private set; }

        /// <summary>
        /// True while the session is the limited create-passkey session (issued by the server's
        /// create-passkey sign-in). Layouts hide navigation and AuthGate keeps the user on the
        /// passkey page until a passkey is registered; the server enforces the same limit on API calls.
        /// </summary>
        public virtual bool PasskeySetupOnly { get; private set; }

        public virtual bool IsSignedIn => SessionToken is not null;

        /// <summary>
        /// True when the server reports this request arrived on the Local network tier. Held in memory only and
        /// refreshed from the server; never persisted, so a stale stored value cannot unlock Local-only pages.
        /// Only used to hide navigation and gate pages - the server still enforces the same rule on every call.
        /// </summary>
        public virtual bool IsLocalTier { get; private set; }

        /// <summary>True when the server reports the session was established with an operator passkey. In-memory only, as <see cref="IsLocalTier"/>.</summary>
        public virtual bool IsPasskeySession { get; private set; }

        /// <summary>
        /// True when the server reports the session was established with a password login. Such a session confirms
        /// destructive changes by re-entering its password, not with a passkey. In-memory only, as <see cref="IsPasskeySession"/>.
        /// </summary>
        public virtual bool IsPasswordSession { get; private set; }

        /// <summary>
        /// True when the operator's role policy requires a second factor, which is a passkey. A password-only session
        /// must upgrade to a passkey before it can confirm a destructive change. In-memory only.
        /// </summary>
        public virtual bool IsTwoFactorRequired { get; private set; }

        /// <summary>
        /// The restricted settings pages a Super Admin has opened to admins. Only read for Admin sessions (a Super Admin
        /// always has access, a lower role never does). In-memory only, like <see cref="IsLocalTier"/>.
        /// </summary>
        public virtual RestrictedSettings SettingsOpenToAdmin { get; private set; }

        /// <summary>
        /// True once the server has reported the tier and credential kind for the current session. False after sign-in
        /// until the next check, so page guards keep Local-only pages closed rather than trusting a stale answer.
        /// </summary>
        public virtual bool ServerKindKnown { get; private set; }

        /// <summary>Fired when the server rejects the session token as invalid or expired.</summary>
        public event Action? AuthenticationExpired;

        /// <summary>Fired whenever the session's server-reported tier, credential kind or known-state changes.</summary>
        public event Action? ServerKindChanged;

        /// <summary>
        /// Loads the stored session and asks the server for its kind, once per circuit. Concurrent callers share one
        /// load, so none of them sees a half-loaded session.
        /// </summary>
        public virtual Task EnsureLoadedAsync()
        {
            _loadTask ??= LoadAsync();
            return _loadTask;
        }

        /// <summary>
        /// Asks the server for the session's tier and kind if that answer is not yet known. Safe to call on every
        /// page load: it costs a round trip only after sign-in, sign-out or a failed earlier check.
        /// </summary>
        public async Task EnsureServerKindAsync()
        {
            if (!ServerKindKnown)
            {
                _ = await RefreshPasskeySetupOnlyFromServerAsync();
            }
        }

        private async Task LoadAsync()
        {
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
                    PasskeySetupOnly = stored.PasskeySetupOnly;
                }
            }
            catch (JSException)
            {
                // Pre-render pass or a JS interop call before the circuit is fully connected - the
                // caller will simply see IsSignedIn = false until the next real load attempt.
            }

            _ = await RefreshPasskeySetupOnlyFromServerAsync();
        }

        /// <summary>
        /// Re-checks the session kind with the server (e.g. after a passkey registration that ends a setup session).
        /// On failure the stored flag is kept.
        /// </summary>
        public Task RefreshPasskeySetupOnlyAsync()
        {
            return RefreshPasskeySetupOnlyFromServerAsync();
        }

        /// <summary>
        /// Checks with the server that the session is still valid and refreshes the stored setup-only flag.
        /// Returns false only when the server answers 401 Unauthorized, which means the session has ended
        /// (e.g. a setup session closed after passkey registration). Any other outcome - success, another
        /// status, an exception, or a missing client factory/token - returns true so transient failures
        /// never sign the operator out.
        /// </summary>
        public Task<bool> ConfirmSessionAsync()
        {
            return RefreshPasskeySetupOnlyFromServerAsync();
        }

        /// <summary>
        /// Asks the server which kind of session the token is, so the layout follows the token rather than
        /// browser storage. On any failure the stored flag stays in place and the session is not cleared.
        /// Returns false only when the server answers 401 (session ended); true otherwise.
        /// </summary>
        private async Task<bool> RefreshPasskeySetupOnlyFromServerAsync()
        {
            if (SessionToken is null)
            {
                return true;
            }

            ISelfApiHttpClientFactory? clientFactory = _services?.GetService<ISelfApiHttpClientFactory>();
            if (clientFactory is null)
            {
                // No server to ask on this host (e.g. MAUI). Mark the answer known with the most restrictive tier so
                // Local-only pages stay hidden rather than the guard waiting forever.
                ApplyServerSessionKind(localTier: false, passkeySession: false, known: true);
                return true;
            }

            bool serverPasskeySetupOnly;
            try
            {
                using HttpClient client = clientFactory.CreateClient(SessionToken);
                using HttpResponseMessage response = await client.GetAsync("api/v1/auth/session-kind");
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    return false;
                }

                if (!response.IsSuccessStatusCode)
                {
                    return true;
                }

                SessionKindResponse? kind = await response.Content.ReadFromJsonAsync<SessionKindResponse>();
                if (kind is null)
                {
                    return true;
                }

                serverPasskeySetupOnly = kind.PasskeySetupOnly;
                await LoadSettingsAccessAsync(client);
                SetServerSessionKind(kind.LocalTier, kind.PasskeySession, kind.PasswordSession, kind.TwoFactorRequired);
            }
            catch (Exception)
            {
                // Best-effort: a network, JSON or host-wiring failure keeps the stored flag as the fallback.
                return true;
            }

            // Persist the server's answer so a reload without the server still shows the right layout.
            if (serverPasskeySetupOnly != PasskeySetupOnly && OperatorId is not null && Role is not null)
            {
                PasskeySetupOnly = serverPasskeySetupOnly;
                await PersistAsync();
            }

            return true;
        }

        /// <summary>
        /// Asks which settings pages admins may use, before the session kind is marked known, so the route guard never
        /// judges those pages on a default. Only admins need it (a Super Admin always has access, a lower role never does);
        /// a failed read leaves the flags false, which hides the pages rather than showing ones the server would refuse.
        /// </summary>
        private async Task LoadSettingsAccessAsync(HttpClient client)
        {
            SettingsOpenToAdmin = RestrictedSettings.None;
            if (Role != nameof(OperatorRole.Admin))
            {
                return;
            }

            try
            {
                SettingsAccessResponse? access = await client.GetFromJsonAsync<SettingsAccessResponse>("api/v1/permissions/settings-access");
                if (access is not null)
                {
                    SettingsOpenToAdmin =
                        (access.ReportsOpenToAdmin ? RestrictedSettings.Reports : RestrictedSettings.None)
                        | (access.EmailOpenToAdmin ? RestrictedSettings.Email : RestrictedSettings.None)
                        | (access.NotificationsOpenToAdmin ? RestrictedSettings.Notifications : RestrictedSettings.None)
                        | (access.LlmOpenToAdmin ? RestrictedSettings.Llm : RestrictedSettings.None);
                }
            }
            catch (Exception)
            {
                // Best-effort, as the session-kind call itself.
            }
        }

        /// <summary>Records which settings pages are open to admins. Public so hosts and tests can seed it.</summary>
        public void SetSettingsAccess(RestrictedSettings openToAdmin)
        {
            SettingsOpenToAdmin = openToAdmin;
            ServerKindChanged?.Invoke();
        }

        /// <summary>
        /// Records the tier and credential kind the server reported for this session. Public so hosts and tests can
        /// seed it; marks the answer as known and notifies listeners.
        /// </summary>
        public void SetServerSessionKind(bool localTier, bool passkeySession, bool passwordSession = false, bool twoFactorRequired = false)
        {
            ApplyServerSessionKind(localTier, passkeySession, known: true, passwordSession, twoFactorRequired);
        }

        public async Task SetAsync(string sessionToken, Guid operatorId, string role, bool mustChangePassword = false, bool passkeySetupOnly = false)
        {
            SessionToken = sessionToken;
            OperatorId = operatorId;
            Role = role;
            MustChangePassword = mustChangePassword;
            PasskeySetupOnly = passkeySetupOnly;

            // A new session's tier is unknown until the server answers. Fail closed until then.
            ApplyServerSessionKind(localTier: false, passkeySession: false, known: false);
            await PersistAsync();
        }

        public async Task ClearAsync()
        {
            SessionToken = null;
            OperatorId = null;
            Role = null;
            MustChangePassword = false;
            PasskeySetupOnly = false;
            SettingsOpenToAdmin = RestrictedSettings.None;
            ApplyServerSessionKind(localTier: false, passkeySession: false, known: false);
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

        private void ApplyServerSessionKind(bool localTier, bool passkeySession, bool known, bool passwordSession = false, bool twoFactorRequired = false)
        {
            IsLocalTier = localTier;
            IsPasskeySession = passkeySession;
            IsPasswordSession = passwordSession;
            IsTwoFactorRequired = twoFactorRequired;
            ServerKindKnown = known;
            ServerKindChanged?.Invoke();
        }

        private async Task PersistAsync()
        {
            try
            {
                string json = JsonSerializer.Serialize(new StoredSession(SessionToken!, OperatorId!.Value, Role!, MustChangePassword, PasskeySetupOnly));
                await _js.InvokeVoidAsync("vfWebAuthn.saveSession", json);
            }
            catch (JSException)
            {
                // wwwroot/scripts/webauthn.js isn't loaded on every host (MAUI's BlazorWebView doesn't reference it -
                // device pairing is server/WebApp-only for now). The in-memory session still works for this
                // circuit; it just won't survive a refresh without vfWebAuthn's localStorage persistence.
            }
        }

        // PasskeySetupOnly defaults to false so a session cookie written before this flag existed still loads.
        private record StoredSession(string SessionToken, Guid OperatorId, string Role, bool MustChangePassword, bool PasskeySetupOnly = false);

        // Response of GET /api/v1/auth/session-kind. Tier and passkey default to false so an older server that omits them fails closed.
        private sealed record SettingsAccessResponse(bool ReportsOpenToAdmin, bool EmailOpenToAdmin, bool NotificationsOpenToAdmin, bool LlmOpenToAdmin = false);

        private sealed record SessionKindResponse(bool PasskeySetupOnly, bool LocalTier = false, bool PasskeySession = false, bool PasswordSession = false, bool TwoFactorRequired = false);
    }
}
