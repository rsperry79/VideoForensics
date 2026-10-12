using VideoForensics.Data.Common.Entities;
using VideoForensics.WebApp.Api;

using Xunit;

namespace VideoForensics.WebApp.Tests
{
    /// <summary>
    /// Covers the step-up credential decision for POST /api/v1/auth/webauthn/stepup-complete.
    /// The endpoint is an inline lambda, so its decision is tested through StepUpCredentialPolicy.
    /// </summary>
    public class StepUpOperatorPasskeyTests
    {
        private static OperatorCredential CreateCredential(Guid operatorId, bool isApproved = true, bool isRevoked = false)
        {
            return new OperatorCredential
            {
                Id = Guid.NewGuid(),
                OperatorId = operatorId,
                Label = "Test passkey",
                WebAuthnCredentialId = "Y3JlZC1pZA==",
                WebAuthnPublicKey = new byte[] { 1, 2, 3 },
                IsApproved = isApproved,
                RevokedAtUtc = isRevoked ? DateTime.UtcNow : null
            };
        }

        [Fact]
        public void StepUpComplete_OperatorPasskeySession_ApprovedOwnCredential_Succeeds()
        {
            Guid operatorId = Guid.NewGuid();
            OperatorCredential credential = CreateCredential(operatorId, isApproved: true);

            bool allowed = StepUpCredentialPolicy.IsAcceptableOperatorPasskey(credential, operatorId);

            Assert.True(allowed);
        }

        [Fact]
        public void StepUpComplete_OperatorPasskeySession_CredentialOfAnotherOperator_Returns401()
        {
            Guid sessionOperatorId = Guid.NewGuid();
            OperatorCredential credential = CreateCredential(Guid.NewGuid(), isApproved: true);

            bool allowed = StepUpCredentialPolicy.IsAcceptableOperatorPasskey(credential, sessionOperatorId);

            Assert.False(allowed);
        }

        [Fact]
        public void StepUpComplete_OperatorPasskeySession_UnapprovedCredential_Returns401()
        {
            Guid operatorId = Guid.NewGuid();
            OperatorCredential credential = CreateCredential(operatorId, isApproved: false);

            bool allowed = StepUpCredentialPolicy.IsAcceptableOperatorPasskey(credential, operatorId);

            Assert.False(allowed);
        }

        [Fact]
        public void StepUpComplete_OperatorPasskeySession_RevokedCredential_Returns401()
        {
            Guid operatorId = Guid.NewGuid();
            OperatorCredential credential = CreateCredential(operatorId, isApproved: true, isRevoked: true);

            bool allowed = StepUpCredentialPolicy.IsAcceptableOperatorPasskey(credential, operatorId);

            Assert.False(allowed);
        }

        [Fact]
        public void StepUpComplete_OperatorPasskeySession_MissingCredential_Returns401()
        {
            bool allowed = StepUpCredentialPolicy.IsAcceptableOperatorPasskey(null, Guid.NewGuid());

            Assert.False(allowed);
        }

        [Fact]
        public void StepUpComplete_DeviceSession_UnchangedBehavior()
        {
            // Device rule is unchanged: an active paired device with a public key is usable,
            // while a missing, revoked, or key-less device is rejected.
            var activeDevice = new PairedDevice
            {
                Id = Guid.NewGuid(),
                OperatorId = Guid.NewGuid(),
                DeviceName = "Test device",
                WebAuthnCredentialId = "ZGV2LWNyZWQ=",
                WebAuthnPublicKey = new byte[] { 1, 2, 3 }
            };
            var revokedDevice = new PairedDevice
            {
                Id = Guid.NewGuid(),
                OperatorId = activeDevice.OperatorId,
                DeviceName = "Revoked device",
                WebAuthnCredentialId = "ZGV2LWNyZWQ=",
                WebAuthnPublicKey = new byte[] { 1, 2, 3 },
                RevokedAtUtc = DateTime.UtcNow
            };
            var keylessDevice = new PairedDevice
            {
                Id = Guid.NewGuid(),
                OperatorId = activeDevice.OperatorId,
                DeviceName = "Keyless device",
                WebAuthnCredentialId = null,
                WebAuthnPublicKey = null
            };

            Assert.True(StepUpCredentialPolicy.IsUsablePairedDevice(activeDevice));
            Assert.False(StepUpCredentialPolicy.IsUsablePairedDevice(revokedDevice));
            Assert.False(StepUpCredentialPolicy.IsUsablePairedDevice(keylessDevice));
            Assert.False(StepUpCredentialPolicy.IsUsablePairedDevice(null));
        }
    }
}
