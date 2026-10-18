using VideoForensics.Data.Common.Entities;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// Decides whether the credential behind a step-up assertion may satisfy the current session.
    /// Used by POST /api/v1/auth/webauthn/stepup-complete for both device and operator-passkey sessions.
    /// </summary>
    internal static class StepUpCredentialPolicy
    {
        /// <summary>
        /// Device session rule: the paired device must be active and carry a WebAuthn public key.
        /// </summary>
        public static bool IsUsablePairedDevice(PairedDevice? device)
        {
            return device != null && device.IsActive && device.WebAuthnPublicKey != null;
        }

        /// <summary>
        /// Operator-passkey session rule: the credential must exist, carry a public key, be approved and not revoked,
        /// and belong to the session's own operator. A passkey owned by any other operator never satisfies step-up.
        /// </summary>
        public static bool IsAcceptableOperatorPasskey(OperatorCredential? credential, Guid sessionOperatorId)
        {
            return credential != null
                && credential.WebAuthnPublicKey != null
                && credential.OperatorId == sessionOperatorId
                && credential.IsApproved
                && credential.IsActive;
        }

        /// <summary>
        /// The credential ID a step-up challenge may offer in an operator-passkey session: only the passkey the session
        /// signed in with. Null when that passkey no longer satisfies the session, so the caller fails closed instead of
        /// offering every credential to the browser.
        /// </summary>
        public static string? OperatorPasskeyAllowedCredentialId(OperatorCredential? sessionPasskey, Guid sessionOperatorId)
        {
            return IsAcceptableOperatorPasskey(sessionPasskey, sessionOperatorId) ? sessionPasskey!.WebAuthnCredentialId : null;
        }
    }
}
