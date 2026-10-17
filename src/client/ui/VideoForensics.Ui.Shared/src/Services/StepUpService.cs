namespace VideoForensics.Ui.Shared.Services
{
    /// <summary>
    /// Obtains the step-up token a destructive or system change needs. A passkey session confirms with a passkey
    /// challenge; a password session re-enters its password through the registered prompt. Read-only views never
    /// call this.
    /// </summary>
    public sealed class StepUpService
    {
        private readonly WebAuthnClient _webAuthn;
        private readonly PairedSessionState _session;
        private Func<Task<string?>>? _passwordPrompt;

        public StepUpService(WebAuthnClient webAuthn, PairedSessionState session)
        {
            _webAuthn = webAuthn;
            _session = session;
        }

        /// <summary>
        /// Registers how to ask a password-session operator for their password. The prompt returns null when the operator
        /// cancels. The StepUpPasswordDialog component registers itself here.
        /// </summary>
        public void RegisterPasswordPrompt(Func<Task<string?>> prompt)
        {
            _passwordPrompt = prompt;
        }

        /// <summary>
        /// Returns a fresh step-up token for the current session. Throws when the operator cancels the prompt or the
        /// server rejects the confirmation, so callers stop the action.
        /// </summary>
        public async Task<string> GetStepUpTokenAsync()
        {
            // The tier answer decides which confirmation applies, so wait for it rather than guess from the default.
            await _session.EnsureServerKindAsync();
            string token = _session.SessionToken ?? throw new InvalidOperationException("Not signed in.");

            if (!_session.IsPasswordSession)
            {
                return await _webAuthn.StepUpAsync(token);
            }

            if (_passwordPrompt is null)
            {
                throw new InvalidOperationException("No password prompt is available.");
            }

            string? password = await _passwordPrompt();
            if (string.IsNullOrEmpty(password))
            {
                throw new InvalidOperationException("Password confirmation was cancelled.");
            }

            return await _webAuthn.StepUpWithPasswordAsync(token, password);
        }
    }
}
