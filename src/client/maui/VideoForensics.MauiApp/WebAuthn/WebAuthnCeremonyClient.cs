using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using static VideoForensics.MauiApp.WebAuthn.WebAuthnNative;

namespace VideoForensics.MauiApp.WebAuthn
{
    /// <summary>
    /// Drives a real Windows Hello passkey ceremony via webauthn.dll (see WebAuthnNative's doc
    /// comment for the P/Invoke risk note - THIS CLASS IS UNVERIFIED end-to-end; no tool available
    /// when it was written could drive/observe the native Windows Hello prompt it triggers) and
    /// produces JSON shaped to match Fido2NetLib's AuthenticatorAttestationRawResponse/
    /// AuthenticatorAssertionRawResponse, ready to POST to the server's
    /// /api/pairing/{token}/register/complete or /api/auth/webauthn/assertion-complete endpoints.
    /// </summary>
    public static class WebAuthnCeremonyClient
    {
        private const int WEBAUTHN_ATTESTATION_CONVEYANCE_PREFERENCE_NONE = 1;
        private const int WEBAUTHN_USER_VERIFICATION_REQUIREMENT_PREFERRED = 2;
        private const int WEBAUTHN_AUTHENTICATOR_ATTACHMENT_ANY = 0;

        /// <summary>
        /// Runs a registration ceremony (WebAuthNAuthenticatorMakeCredential) against the server's
        /// CredentialCreateOptions JSON (as returned by POST /api/pairing/{token}/register/options).
        /// Returns a JSON string shaped for Fido2NetLib's AuthenticatorAttestationRawResponse.
        /// </summary>
        public static string MakeCredential(IntPtr windowHandle, JsonElement optionsJson, string origin)
        {
            var rp = optionsJson.GetProperty("rp");
            var user = optionsJson.GetProperty("user");
            var challengeB64Url = optionsJson.GetProperty("challenge").GetString()!;
            var userIdB64Url = user.GetProperty("id").GetString()!;

            var pinnedHandles = new List<GCHandle>();
            try
            {
                var rpInfo = new WEBAUTHN_RP_ENTITY_INFORMATION
                {
                    dwVersion = 1,
                    pwszId = rp.GetProperty("id").GetString()!,
                    pwszName = rp.GetProperty("name").GetString()!
                };

                var userIdBytes = Base64UrlDecode(userIdB64Url);
                var userIdHandle = PinBytes(userIdBytes, pinnedHandles);
                var userInfo = new WEBAUTHN_USER_ENTITY_INFORMATION
                {
                    dwVersion = 1,
                    cbId = userIdBytes.Length,
                    pbId = userIdHandle,
                    pwszName = user.GetProperty("name").GetString()!,
                    pwszDisplayName = user.GetProperty("displayName").GetString()!
                };

                // ES256 (-7) and RS256 (-257) only - the two universally-supported algorithms, kept
                // deliberately minimal for the same "smaller surface, fewer mistakes" reasoning as
                // WebAuthnNative's struct choices.
                var coseParams = new[]
                {
                    new WEBAUTHN_COSE_CREDENTIAL_PARAMETER { dwVersion = 1, pwszCredentialType = "public-key", lAlg = -7 },
                    new WEBAUTHN_COSE_CREDENTIAL_PARAMETER { dwVersion = 1, pwszCredentialType = "public-key", lAlg = -257 }
                };
                var coseParamsPtr = MarshalArray(coseParams, pinnedHandles);
                var pubKeyCredParams = new WEBAUTHN_COSE_CREDENTIAL_PARAMETERS
                {
                    cCredentialParameters = coseParams.Length,
                    pCredentialParameters = coseParamsPtr
                };

                var clientDataJson = BuildClientDataJson("webauthn.create", challengeB64Url, origin);
                var clientDataBytes = Encoding.UTF8.GetBytes(clientDataJson);
                var clientDataHandle = PinBytes(clientDataBytes, pinnedHandles);
                var clientData = new WEBAUTHN_CLIENT_DATA
                {
                    dwVersion = 1,
                    cbClientDataJSON = clientDataBytes.Length,
                    pbClientDataJSON = clientDataHandle,
                    pwszHashAlgId = "SHA-256"
                };

                var options = new WEBAUTHN_AUTHENTICATOR_MAKE_CREDENTIAL_OPTIONS
                {
                    dwVersion = 1,
                    dwTimeoutMilliseconds = 60000,
                    CredentialList = new WEBAUTHN_CREDENTIALS { cCredentials = 0, pCredentials = IntPtr.Zero },
                    cbExtensions = 0,
                    pExtensions = IntPtr.Zero,
                    dwAuthenticatorAttachment = WEBAUTHN_AUTHENTICATOR_ATTACHMENT_ANY,
                    bRequireResidentKey = 0,
                    dwUserVerificationRequirement = WEBAUTHN_USER_VERIFICATION_REQUIREMENT_PREFERRED,
                    dwAttestationConveyancePreference = WEBAUTHN_ATTESTATION_CONVEYANCE_PREFERENCE_NONE,
                    dwFlags = 0
                };

                var hr = WebAuthNAuthenticatorMakeCredential(
                    windowHandle, ref rpInfo, ref userInfo, ref pubKeyCredParams, ref clientData, ref options,
                    out var attestationPtr);

                if (hr != 0 || attestationPtr == IntPtr.Zero)
                {
                    throw new WebAuthnCeremonyException($"WebAuthNAuthenticatorMakeCredential failed: HRESULT 0x{hr:X8} ({ReadErrorName(hr)})");
                }

                try
                {
                    var attestation = Marshal.PtrToStructure<WEBAUTHN_CREDENTIAL_ATTESTATION>(attestationPtr);
                    var credentialId = CopyBytes(attestation.pbCredentialId, attestation.cbCredentialId);
                    var attestationObject = CopyBytes(attestation.pbAttestationObject, attestation.cbAttestationObject);

                    return JsonSerializer.Serialize(new
                    {
                        id = Base64UrlEncode(credentialId),
                        rawId = Convert.ToBase64String(credentialId),
                        type = "public-key",
                        response = new
                        {
                            attestationObject = Convert.ToBase64String(attestationObject),
                            clientDataJson = Convert.ToBase64String(clientDataBytes),
                            clientDataJSON = Convert.ToBase64String(clientDataBytes)
                        }
                    });
                }
                finally
                {
                    WebAuthNFreeCredentialAttestation(attestationPtr);
                }
            }
            finally
            {
                foreach (var handle in pinnedHandles)
                {
                    if (handle.IsAllocated) handle.Free();
                }
            }
        }

        /// <summary>
        /// Runs an authentication ceremony (WebAuthNAuthenticatorGetAssertion) against the server's
        /// AssertionOptions JSON (as returned by POST /api/auth/webauthn/assertion-options or
        /// /api/auth/webauthn/stepup-complete's preceding options call). Returns JSON shaped for
        /// Fido2NetLib's AuthenticatorAssertionRawResponse.
        /// </summary>
        public static string GetAssertion(IntPtr windowHandle, JsonElement optionsJson, string rpId, string origin)
        {
            var challengeB64Url = optionsJson.GetProperty("challenge").GetString()!;

            var pinnedHandles = new List<GCHandle>();
            try
            {
                var clientDataJson = BuildClientDataJson("webauthn.get", challengeB64Url, origin);
                var clientDataBytes = Encoding.UTF8.GetBytes(clientDataJson);
                var clientDataHandle = PinBytes(clientDataBytes, pinnedHandles);
                var clientData = new WEBAUTHN_CLIENT_DATA
                {
                    dwVersion = 1,
                    cbClientDataJSON = clientDataBytes.Length,
                    pbClientDataJSON = clientDataHandle,
                    pwszHashAlgId = "SHA-256"
                };

                WEBAUTHN_CREDENTIALS allowList = default;
                if (optionsJson.TryGetProperty("allowCredentials", out var allowCredsJson) && allowCredsJson.GetArrayLength() > 0)
                {
                    var creds = allowCredsJson.EnumerateArray()
                        .Select(c =>
                        {
                            var idBytes = Base64UrlDecode(c.GetProperty("id").GetString()!);
                            var idHandle = PinBytes(idBytes, pinnedHandles);
                            return new WEBAUTHN_CREDENTIAL
                            {
                                dwVersion = 1,
                                cbId = idBytes.Length,
                                pbId = idHandle,
                                pwszCredentialType = "public-key"
                            };
                        })
                        .ToArray();
                    var credsPtr = MarshalArray(creds, pinnedHandles);
                    allowList = new WEBAUTHN_CREDENTIALS { cCredentials = creds.Length, pCredentials = credsPtr };
                }

                var options = new WEBAUTHN_AUTHENTICATOR_GET_ASSERTION_OPTIONS
                {
                    dwVersion = 1,
                    dwTimeoutMilliseconds = 60000,
                    CredentialList = allowList,
                    cbExtensions = 0,
                    pExtensions = IntPtr.Zero,
                    dwAuthenticatorAttachment = WEBAUTHN_AUTHENTICATOR_ATTACHMENT_ANY,
                    dwUserVerificationRequirement = WEBAUTHN_USER_VERIFICATION_REQUIREMENT_PREFERRED,
                    dwFlags = 0
                };

                var hr = WebAuthNAuthenticatorGetAssertion(windowHandle, rpId, ref clientData, ref options, out var assertionPtr);

                if (hr != 0 || assertionPtr == IntPtr.Zero)
                {
                    throw new WebAuthnCeremonyException($"WebAuthNAuthenticatorGetAssertion failed: HRESULT 0x{hr:X8} ({ReadErrorName(hr)})");
                }

                try
                {
                    var assertion = Marshal.PtrToStructure<WEBAUTHN_ASSERTION>(assertionPtr);
                    var authenticatorData = CopyBytes(assertion.pbAuthenticatorData, assertion.cbAuthenticatorData);
                    var signature = CopyBytes(assertion.pbSignature, assertion.cbSignature);
                    var userHandle = assertion.cbUserId > 0 ? CopyBytes(assertion.pbUserId, assertion.cbUserId) : Array.Empty<byte>();
                    var credentialId = CopyBytes(assertion.Credential.pbId, assertion.Credential.cbId);

                    return JsonSerializer.Serialize(new
                    {
                        id = Base64UrlEncode(credentialId),
                        rawId = Convert.ToBase64String(credentialId),
                        type = "public-key",
                        response = new
                        {
                            authenticatorData = Convert.ToBase64String(authenticatorData),
                            signature = Convert.ToBase64String(signature),
                            userHandle = userHandle.Length > 0 ? Convert.ToBase64String(userHandle) : null,
                            clientDataJson = Convert.ToBase64String(clientDataBytes),
                            clientDataJSON = Convert.ToBase64String(clientDataBytes)
                        }
                    });
                }
                finally
                {
                    WebAuthNFreeAssertion(assertionPtr);
                }
            }
            finally
            {
                foreach (var handle in pinnedHandles)
                {
                    if (handle.IsAllocated) handle.Free();
                }
            }
        }

        private static string BuildClientDataJson(string type, string challengeB64Url, string origin) =>
            JsonSerializer.Serialize(new { type, challenge = challengeB64Url, origin, crossOrigin = false });

        private static byte[] CopyBytes(IntPtr ptr, int length)
        {
            if (ptr == IntPtr.Zero || length == 0) return Array.Empty<byte>();
            var bytes = new byte[length];
            Marshal.Copy(ptr, bytes, 0, length);
            return bytes;
        }

        private static IntPtr PinBytes(byte[] bytes, List<GCHandle> tracker)
        {
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            tracker.Add(handle);
            return handle.AddrOfPinnedObject();
        }

        private static IntPtr MarshalArray<T>(T[] items, List<GCHandle> tracker) where T : struct
        {
            var handle = GCHandle.Alloc(items, GCHandleType.Pinned);
            tracker.Add(handle);
            return handle.AddrOfPinnedObject();
        }

        private static string ReadErrorName(int hr)
        {
            try
            {
                var ptr = WebAuthNGetErrorName(hr);
                return ptr == IntPtr.Zero ? "unknown" : Marshal.PtrToStringUni(ptr) ?? "unknown";
            }
            catch
            {
                return "unknown";
            }
        }

        private static byte[] Base64UrlDecode(string input)
        {
            var s = input.Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4)
            {
                case 2: s += "=="; break;
                case 3: s += "="; break;
            }
            return Convert.FromBase64String(s);
        }

        private static string Base64UrlEncode(byte[] bytes) =>
            Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    public class WebAuthnCeremonyException : Exception
    {
        public WebAuthnCeremonyException(string message) : base(message) { }
    }
}
