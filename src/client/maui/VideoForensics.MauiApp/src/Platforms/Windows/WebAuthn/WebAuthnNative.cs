using System.Runtime.InteropServices;

namespace VideoForensics.MauiApp.WebAuthn
{
    /// <summary>
    /// Raw P/Invoke declarations for Windows' native webauthn.dll (Windows 10 1903+), which backs
    /// Windows Hello passkey ceremonies. There is no first-party .NET/MAUI wrapper for this API - a
    /// spike (this session) confirmed the DLL is P/Invoke-reachable on this SDK/OS combination
    /// (WebAuthNGetApiVersionNumber returned a real version, WebAuthNIsUserVerifyingPlatformAuthenticatorAvailable
    /// returned true) before this fuller wrapper was written.
    ///
    /// DELIBERATELY uses only the WEBAUTHN_API_VERSION_1 struct shapes (unchanged since Windows 10
    /// 1903, the most stable and thoroughly documented revision) rather than the full surface of the
    /// newer API version this machine's DLL reports supporting (9) - the native DLL is backward
    /// compatible with older, smaller struct versions via each struct's own dwVersion field, and a
    /// smaller struct surface means fewer fields that could have a silent layout mistake. This
    /// trades some newer features (resident-key preference nuance, hybrid transport, etc.) for a
    /// meaningfully smaller and better-documented interop surface - a deliberate risk reduction,
    /// not an oversight.
    ///
    /// RISK NOTE, stated plainly rather than glossed over: this is native struct marshaling code
    /// that has NOT been verified end-to-end against a real Windows Hello ceremony - no tool
    /// available in the environment this was written in can drive or observe a native Windows Hello
    /// prompt. A field-order or size mistake in a struct here would very likely either throw
    /// immediately (most likely, since P/Invoke marshaling failures are usually loud) or, in the
    /// worst case, produce a wrong/corrupted result. Treat this file as unverified until it has been
    /// exercised by hand: pair a real MAUI Windows build against a running VideoForensics.WebApp and
    /// confirm the full registration ceremony actually completes and the server accepts the result.
    /// </summary>
    internal static class WebAuthnNative
    {
        private const string Dll = "webauthn.dll";

        [DllImport(Dll, CallingConvention = CallingConvention.Winapi)]
        public static extern int WebAuthNGetApiVersionNumber();

        [DllImport(Dll, CallingConvention = CallingConvention.Winapi)]
        public static extern int WebAuthNIsUserVerifyingPlatformAuthenticatorAvailable(out bool pbIsUserVerifyingPlatformAuthenticatorAvailable);

        [DllImport(Dll, CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Unicode)]
        public static extern int WebAuthNAuthenticatorMakeCredential(
            IntPtr hWnd,
            ref WEBAUTHN_RP_ENTITY_INFORMATION pRpInformation,
            ref WEBAUTHN_USER_ENTITY_INFORMATION pUserInformation,
            ref WEBAUTHN_COSE_CREDENTIAL_PARAMETERS pPubKeyCredParams,
            ref WEBAUTHN_CLIENT_DATA pWebAuthNClientData,
            ref WEBAUTHN_AUTHENTICATOR_MAKE_CREDENTIAL_OPTIONS pWebAuthNMakeCredentialOptions,
            out IntPtr ppWebAuthNCredentialAttestation);

        [DllImport(Dll, CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Unicode)]
        public static extern int WebAuthNAuthenticatorGetAssertion(
            IntPtr hWnd,
            [MarshalAs(UnmanagedType.LPWStr)] string pwszRpId,
            ref WEBAUTHN_CLIENT_DATA pWebAuthNClientData,
            ref WEBAUTHN_AUTHENTICATOR_GET_ASSERTION_OPTIONS pWebAuthNGetAssertionOptions,
            out IntPtr ppWebAuthNAssertion);

        [DllImport(Dll, CallingConvention = CallingConvention.Winapi)]
        public static extern void WebAuthNFreeCredentialAttestation(IntPtr pWebAuthNCredentialAttestation);

        [DllImport(Dll, CallingConvention = CallingConvention.Winapi)]
        public static extern void WebAuthNFreeAssertion(IntPtr pWebAuthNAssertion);

        [DllImport(Dll, CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Unicode)]
        public static extern IntPtr WebAuthNGetErrorName(int hr);

        // ---- WEBAUTHN_API_VERSION_1 struct shapes (webauthn.h) ----

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WEBAUTHN_RP_ENTITY_INFORMATION
        {
            public int dwVersion;
            [MarshalAs(UnmanagedType.LPWStr)] public string pwszId;
            [MarshalAs(UnmanagedType.LPWStr)] public string pwszName;
            [MarshalAs(UnmanagedType.LPWStr)] public string? pwszIcon;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WEBAUTHN_USER_ENTITY_INFORMATION
        {
            public int dwVersion;
            public int cbId;
            public IntPtr pbId;
            [MarshalAs(UnmanagedType.LPWStr)] public string pwszName;
            [MarshalAs(UnmanagedType.LPWStr)] public string? pwszIcon;
            [MarshalAs(UnmanagedType.LPWStr)] public string pwszDisplayName;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WEBAUTHN_COSE_CREDENTIAL_PARAMETER
        {
            public int dwVersion;
            [MarshalAs(UnmanagedType.LPWStr)] public string pwszCredentialType;
            public int lAlg;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WEBAUTHN_COSE_CREDENTIAL_PARAMETERS
        {
            public int cCredentialParameters;
            public IntPtr pCredentialParameters; // WEBAUTHN_COSE_CREDENTIAL_PARAMETER*
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WEBAUTHN_CLIENT_DATA
        {
            public int dwVersion;
            public int cbClientDataJSON;
            public IntPtr pbClientDataJSON;
            [MarshalAs(UnmanagedType.LPWStr)] public string pwszHashAlgId;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WEBAUTHN_CREDENTIAL
        {
            public int dwVersion;
            public int cbId;
            public IntPtr pbId;
            [MarshalAs(UnmanagedType.LPWStr)] public string pwszCredentialType;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WEBAUTHN_CREDENTIALS
        {
            public int cCredentials;
            public IntPtr pCredentials; // WEBAUTHN_CREDENTIAL*
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WEBAUTHN_AUTHENTICATOR_MAKE_CREDENTIAL_OPTIONS
        {
            public int dwVersion;
            public int dwTimeoutMilliseconds;
            public WEBAUTHN_CREDENTIALS CredentialList; // excludeCredentials
            public int cbExtensions; // WEBAUTHN_EXTENSIONS.cExtensions - zeroed, no extensions used
            public IntPtr pExtensions;
            public int dwAuthenticatorAttachment; // WEBAUTHN_AUTHENTICATOR_ATTACHMENT_*
            public int bRequireResidentKey; // BOOL
            public int dwUserVerificationRequirement; // WEBAUTHN_USER_VERIFICATION_REQUIREMENT_*
            public int dwAttestationConveyancePreference; // WEBAUTHN_ATTESTATION_CONVEYANCE_PREFERENCE_*
            public int dwFlags;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WEBAUTHN_AUTHENTICATOR_GET_ASSERTION_OPTIONS
        {
            public int dwVersion;
            public int dwTimeoutMilliseconds;
            public WEBAUTHN_CREDENTIALS CredentialList; // allowCredentials
            public int cbExtensions;
            public IntPtr pExtensions;
            public int dwAuthenticatorAttachment;
            public int dwUserVerificationRequirement;
            public int dwFlags;
        }

        // Native result structs - fields read via Marshal.PtrToStructure from the out IntPtr the
        // Make/Get calls return; freed via WebAuthNFreeCredentialAttestation/WebAuthNFreeAssertion.

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WEBAUTHN_CREDENTIAL_ATTESTATION
        {
            public int dwVersion;
            [MarshalAs(UnmanagedType.LPWStr)] public string pwszFormatType;
            public int cbAuthenticatorData;
            public IntPtr pbAuthenticatorData;
            public int cbAttestation;
            public IntPtr pbAttestation;
            public int dwAttestationDecodeType;
            public IntPtr pvAttestationDecode;
            public int cbAttestationObject;
            public IntPtr pbAttestationObject;
            public int cbCredentialId;
            public IntPtr pbCredentialId;
            // Extension/transport/flags fields from later versions intentionally omitted - not read.
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WEBAUTHN_ASSERTION
        {
            public int dwVersion;
            public int cbAuthenticatorData;
            public IntPtr pbAuthenticatorData;
            public int cbSignature;
            public IntPtr pbSignature;
            public WEBAUTHN_CREDENTIAL Credential;
            public int cbUserId;
            public IntPtr pbUserId;
        }
    }
}
