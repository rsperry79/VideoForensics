
using Fido2NetLib;

using Fido2NetLib.Objects;

using VideoForensics.Client.WebApp.Auth;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Auth
{
    public class PasskeyUserHandleVerifierTests
    {
        private static readonly byte[] CredentialId = [1, 2, 3, 4];

        [Fact]
        public async Task ForOperator_UserHandleMatchesOperatorId_ReturnsTrue()
        {
            var operatorId = Guid.NewGuid();
            IsUserHandleOwnerOfCredentialIdAsync callback = PasskeyUserHandleVerifier.ForOperator(operatorId);

            bool result = await callback(
                new IsUserHandleOwnerOfCredentialIdParams(CredentialId, operatorId.ToByteArray()),
                CancellationToken.None);

            Assert.True(result);
        }

        [Fact]
        public async Task ForOperator_UserHandleFromDifferentOperator_ReturnsFalse()
        {
            var operatorId = Guid.NewGuid();
            var otherOperatorId = Guid.NewGuid();
            IsUserHandleOwnerOfCredentialIdAsync callback = PasskeyUserHandleVerifier.ForOperator(operatorId);

            bool result = await callback(
                new IsUserHandleOwnerOfCredentialIdParams(CredentialId, otherOperatorId.ToByteArray()),
                CancellationToken.None);

            Assert.False(result);
        }

        [Fact]
        public async Task ForOperator_NullUserHandle_ReturnsTrue()
        {
            // Non-discoverable login: no user handle is sent. Identity comes from the credential ID lookup
            // plus the signature check against the stored key, so there is no claim to contradict.
            IsUserHandleOwnerOfCredentialIdAsync callback = PasskeyUserHandleVerifier.ForOperator(Guid.NewGuid());

            bool result = await callback(
                new IsUserHandleOwnerOfCredentialIdParams(CredentialId, null),
                CancellationToken.None);

            Assert.True(result);
        }

        [Fact]
        public void IsOwner_UserHandleIsPrefixOfOperatorId_ReturnsFalse()
        {
            var operatorId = Guid.NewGuid();
            byte[] truncated = operatorId.ToByteArray()[..8];

            Assert.False(PasskeyUserHandleVerifier.IsOwner(operatorId, truncated));
        }

        [Fact]
        public void IsOwner_OperatorIdWithOneByteChanged_ReturnsFalse()
        {
            var operatorId = Guid.NewGuid();
            byte[] tampered = operatorId.ToByteArray();
            tampered[15] ^= 0xFF;

            Assert.False(PasskeyUserHandleVerifier.IsOwner(operatorId, tampered));
        }
    }
}
