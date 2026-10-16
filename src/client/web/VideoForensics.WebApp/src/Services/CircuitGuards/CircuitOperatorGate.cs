using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.Ui.Shared.Layout;
using VideoForensics.Ui.Shared.Services;
using VideoForensics.WebApp.Contracts;

namespace VideoForensics.WebApp.Services.CircuitGuards
{
    /// <summary>
    /// Evaluates the same <see cref="PageAccessRules"/> the UI uses, but on the server and from validated state:
    /// the role and passkey kind come from the session token (re-validated here, not trusted from the browser),
    /// and the tier comes from the circuit's own captured network context.
    /// </summary>
    public sealed class CircuitOperatorGate(
        CircuitScope circuit,
        PairedSessionState session,
        ISessionTokenService tokens,
        SessionNetworkContext network,
        IStepUpAuthService stepUp) : ICircuitOperatorGate
    {
        public bool IsStepUpValid(string? stepUpToken)
        {
            if (!circuit.IsOpen)
            {
                return true;
            }

            string? token = session.SessionToken;
            SessionPrincipal? principal = token is null ? null : tokens.Validate(token);
            if (principal is null || stepUpToken is null)
            {
                return false;
            }

            // The endpoint filter binds a step-up to the paired-device claim when present, else to the operator. A
            // passkey session's paired-device claim is its credential, so mirror that choice here.
            Guid subject = principal.CredentialId ?? principal.OperatorId;
            return stepUp.Validate(stepUpToken, subject);
        }

        public bool Allows(AccessPolicy policy)
        {
            if (!circuit.IsOpen)
            {
                return true;
            }

            string? token = session.SessionToken;
            SessionPrincipal? principal = token is null ? null : tokens.Validate(token);
            if (principal is null)
            {
                return false;
            }

            var context = new NavContext(
                IsSignedIn: true,
                Role: principal.Role,
                AppLockSupported: false,
                IsLocalTier: network.Tier == NetworkTier.Local,
                IsPasskeySession: principal.Kind == CredentialKind.OperatorPasskey);

            return PageAccessRules.Allows(policy, context);
        }
    }
}
