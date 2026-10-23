using Microsoft.Extensions.Localization;

using VideoForensics.Client.WebApp.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Hosting;
using VideoForensics.Client.Ui.Shared.Layout;
using VideoForensics.Client.Ui.Shared.Resources;
using VideoForensics.Client.Ui.Shared.Services;

namespace VideoForensics.Client.WebApp.Services.CircuitGuards
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
        IStepUpAuthService stepUp,
        IStringLocalizer<SharedResources> localizer) : ICircuitOperatorGate
    {
        public string Denial(AccessPolicy policy)
        {
            string key = policy switch
            {
                AccessPolicy.ReadOnly => "Denied_ReadOnly",
                AccessPolicy.Review => "Denied_Review",
                AccessPolicy.Admin => "Denied_Admin",
                AccessPolicy.SuperAdmin => "Denied_SuperAdmin",
                AccessPolicy.SuperAdminLocal => "Denied_LocalSuperAdmin",
                AccessPolicy.SuperAdminLocalOrPasskey => "Denied_LocalSuperAdmin",
                _ => "Denied_ReadOnly",
            };
            return localizer[key].Value;
        }

        public Guid? OperatorId
        {
            get
            {
                if (!circuit.IsOpen)
                {
                    return null;
                }

                string? token = session.SessionToken;
                return token is null ? null : tokens.Validate(token)?.OperatorId;
            }
        }

        public string StepUpDenial()
        {
            return localizer["Denied_StepUp"].Value;
        }

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
