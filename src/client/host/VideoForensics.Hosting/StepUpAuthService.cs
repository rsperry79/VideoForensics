using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace VideoForensics.Hosting
{
    /// <summary>
    /// Step-up re-authentication (plan §5.7): a small number of high-risk actions (device
    /// revocation, Operator deactivation, factory reset, widening the network tier, releasing a
    /// Legal Hold) require a FRESH passkey assertion at the moment of the action, not merely an
    /// already-open session - answering "is it still really them, right now, for this one
    /// dangerous thing", distinct from and layered on top of session-start verification (§5.11)
    /// which only answers "who is this, as of when the session began".
    ///
    /// ESCALATION-FLAGGED (plan §10): deliberately kept as its own separate mechanism rather than
    /// folded into session verification - collapsing the two would quietly weaken the step-up
    /// guarantee for actions performed early in a session (a valid-but-stale session would then
    /// satisfy both checks with the same one verification).
    /// </summary>
    public interface IStepUpAuthService
    {
        /// <summary>Issues a step-up token for a specific paired device, valid briefly (2 minutes) and for that device only.</summary>
        string IssueToken(Guid pairedDeviceId);

        /// <summary>True if the token is valid, unexpired, and was issued for this exact paired device.</summary>
        bool Validate(string token, Guid pairedDeviceId);
    }

    public class StepUpAuthService : IStepUpAuthService
    {
        private static readonly TimeSpan StepUpLifetime = TimeSpan.FromMinutes(2);
        private readonly IDataProtector _protector;

        private record StepUpPayload(Guid PairedDeviceId, DateTime ExpiresAtUtc);

        public StepUpAuthService(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector("VideoForensics.StepUpTokens.v1");
        }

        public string IssueToken(Guid pairedDeviceId)
        {
            var payload = new StepUpPayload(pairedDeviceId, DateTime.UtcNow + StepUpLifetime);
            return _protector.Protect(JsonSerializer.Serialize(payload));
        }

        public bool Validate(string token, Guid pairedDeviceId)
        {
            try
            {
                var payload = JsonSerializer.Deserialize<StepUpPayload>(_protector.Unprotect(token));
                return payload != null && payload.PairedDeviceId == pairedDeviceId && payload.ExpiresAtUtc > DateTime.UtcNow;
            }
            catch
            {
                return false;
            }
        }
    }
}
