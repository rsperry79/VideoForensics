using Fido2NetLib;

namespace VideoForensics.Client.WebApp.Auth
{
    /// <summary>
    /// Builds the Fido2NetLib user-handle ownership callback for passkey assertions.
    /// </summary>
    /// <remarks>
    /// Fido2NetLib 4.2.0 invokes this callback only when the assertion carries a user handle. A
    /// non-discoverable login may send none, and the library does not reject that case by itself.
    /// A null handle is therefore accepted: the credential was already resolved from its credential ID,
    /// and the signature was verified against that credential's stored public key, so there is no
    /// identity claim to contradict. A non-null handle must equal the owning operator's Id bytes.
    /// </remarks>
    internal static class PasskeyUserHandleVerifier
    {
        /// <summary>
        /// Returns the callback to pass as <c>MakeAssertionParams.IsUserHandleOwnerOfCredentialIdCallback</c>
        /// for a credential owned by <paramref name="operatorId"/>.
        /// </summary>
        public static IsUserHandleOwnerOfCredentialIdAsync ForOperator(Guid operatorId)
        {
            return (args, _) => Task.FromResult(IsOwner(operatorId, args.UserHandle));
        }

        /// <summary>
        /// Returns true when <paramref name="userHandle"/> is null (no identity claim was sent) or is exactly
        /// the operator's Id bytes (Guid.ToByteArray(), the encoding used as Fido2User.Id at registration).
        /// </summary>
        public static bool IsOwner(Guid operatorId, byte[]? userHandle)
        {
            return userHandle == null || userHandle.AsSpan().SequenceEqual(operatorId.ToByteArray());
        }
    }
}
